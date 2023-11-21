using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Net;
using System;
using System.Net.Sockets;
using System.Text;
using Avalonia.Threading;
using AngouriMath;
using SuperSimpleTcp;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AngouriMath.Extensions;

namespace MathCalc.Views
{
    public partial class MainView : UserControl
    {
        string Name;
        Exercise Exercise;

        public static SimpleTcpServer Server;
        public static SimpleTcpClient Client;

        public List<(string, int)> Solves;

        public MainView()
        {
            InitializeComponent();

            CbType.ItemsSource = Enum.GetValues(typeof(EquationType));
            CbType.SelectedIndex = 0;
        }

        private Entity ParseExpression(string exp)
        {
            return exp.Replace("lim", "limit").Replace("deriv(", "derivative").Replace("int(", "integral").Replace("tg", "tan").Replace("+inf", "+oo").Replace("-inf", "-oo").Replace("inf", "+oo");
        }

        private void Calc_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TbFeedback.Text = "";
                Entity expression = ParseExpression(TbEquation.Text ?? "0");
                Formula.Formula = expression.Latexise();
                Entity answer;

                switch ((EquationType)(Exercise?.Type ?? CbType.SelectedItem ?? EquationType.Calcul))
                {
                    default:
                    case EquationType.Calcul:
                        answer = expression.Simplify();
                        break;
                    case EquationType.Ecuatie:
                        answer = expression.Solve("x");
                        break;
                    case EquationType.Derivare:
                        answer = expression.Differentiate("x").Simplify();
                        break;
                    case EquationType.Integrare:
                        answer = expression.Integrate("x").Simplify();
                        break;
                }

                Entity response = ParseExpression(TbAnswer.Text ?? "0");

                if (Exercise != null && !Exercise.Solved)
                    Exercise.SolveCount++;

                bool correct = response.Simplify().EqualsImprecisely(answer.Simplify());

                // Check user answer
                if (!correct)
                {
                    TbFeedback.Text += "Ai răspuns greșit.";

                    if (Exercise != null)
                        TbFeedback.Text += "\n" + Exercise.SolveCount + " încercări până acum.";

                    if (Exercise?.SolutionVisible != false)
                    {
                        TbFeedback.Text += "\n\nRăspunsul corect:";
                        FormulaFeedback.Formula = answer.Latexise();
                    }
                }
                else
                {
                    TbFeedback.Text = "Ai răspuns correct!";

                    if (Exercise != null)
                        TbFeedback.Text += "\n" + Exercise.SolveCount + " încercări până acum.";

                    if (Exercise != null && !Exercise.Solved)
                    {
                        Exercise.Solved = true;

                        Client?.Send($"Solve,{Exercise.Index},{Exercise.SolveCount},{Name}");
                    }
                }
            }
            catch
            {
                TbFeedback.Text += "\n" + "Nu s-a putut verifica răspunsul tău.";
            }
        }

        private void TbEquation_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                Entity expression = ParseExpression(TbEquation.Text);
                Formula.Formula = expression.Latexise();
            }
            catch { }
        }

        private void TbResponse_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                Entity expression = ParseExpression(TbAnswer.Text);
                FormulaFeedback.Formula = expression.Latexise();
            }
            catch { }
        }

        private void LoadExercise(Exercise ex)
        {
            Exercise = ex;

            LoadExercise();
        }

        private void LoadExercise()
        {
            Exercise.SolveCount = 0;

            TbExercise.Text = "Exercițiu " + Exercise.Index;
            TbEquation.Text = Exercise.Equation;

            CbType.SelectedItem = Exercise.Type;
            CkShowSolve.IsChecked = Exercise.SolutionVisible;

            TbAnswer.Text = string.Empty;
            FormulaFeedback.Formula = string.Empty;

            TbIndication.Text = Exercise.Indication;
            TbFeedback.Text = Exercise.Indication;
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Client = new(TbEquation.Text + ":7778");
                Client.Events.DataReceived += Client_ReceivedData;
                Client.Events.Connected += Client_Connected;
                Client.Events.Disconnected += Client_Disconnected;

                Name = string.IsNullOrWhiteSpace(TbAnswer.Text) ? "Elev" : TbAnswer.Text;

                Client.Connect();
                TbFeedback.Text = "Conectat.";
            }
            catch
            {
                TbFeedback.Text = "Nu s-a putut conecta.";
            }
        }

        private async void Server_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Solves = new();
                LoadExercise(new());
                TbIndication.IsVisible = true;
                CbType.IsVisible = true;

                Server = new("*:7778");
                Server.Events.DataReceived += Server_ReceivedData;
                Server.Events.ClientConnected += Server_ClientConnected;
                Server.Start();

                TbFeedback.Text = "Server profesor pornit.\n\nCod de conectare: " + GetIPAddress().ToString();
                BtConnect.IsEnabled = false;
            }
            catch (Exception Exception)
            {
                TbFeedback.Text = "Eroare server:\n" + Exception.Message;
            }
        }

        public static IPAddress GetIPAddress()
        {
            IPAddress[] hostAddresses = Dns.GetHostAddresses("");

            foreach (IPAddress hostAddress in hostAddresses)
            {
                if (hostAddress.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(hostAddress) &&  // ignore loopback addresses
                    !hostAddress.ToString().StartsWith("169.254."))  // ignore link-local addresses
                    return hostAddress;
            }
            return null;
        }

        private async void Exercise_Click(object sender, RoutedEventArgs e)
        {
            if (Server != null)
            {
                try
                {
                    Solves = new();
                    Exercise.Equation = TbEquation.Text;
                    Exercise.Indication = TbIndication.Text;
                    Exercise.Type = (EquationType)CbType.SelectedItem;
                    Exercise.SolutionVisible = CkShowSolve.IsChecked ?? false;
                    Exercise.Index++;
                    LoadExercise();

                    foreach (var item in Server.GetClients())
                    {
                        await Server.SendAsync(item, $"Exercise,{Exercise.Index},{Exercise.Equation},{Exercise.Indication},{Exercise.Type},{Exercise.SolutionVisible}");
                    };
                }
                catch
                {
                    TbFeedback.Text = "Nu se poate trimite exercițiul.";
                }
            }
            else if (Client != null)
            {
                try
                {
                    await Client.SendAsync($"Exercise,{Exercise?.Index ?? 0 + 1},{TbEquation.Text},Întrebare,{(EquationType)CbType.SelectedItem},true");
                }
                catch
                {
                    TbFeedback.Text = "Nu se poate trimite exercițiul.";
                }
            }
        }

        private Exercise Question;

        private async void BtRefuse_Click(object sender, RoutedEventArgs e)
        {
            QuestionPanel.Opacity = 0;

            await Task.Delay(300);

            QuestionPanel.IsVisible = false;
        }

        private async void BtAccept_Click(object sender, RoutedEventArgs e)
        {
            LoadExercise(Question);
            QuestionPanel.Opacity = 0;

            await Task.Delay(300);

            QuestionPanel.IsVisible = false;
        }

        private async void Disconnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Client.Disconnect();
                Client.Dispose();
                Client = null;
            }
            catch
            {
                TbFeedback.Text = "Pornește server-ul mai întâi.";
            }
        }

        private void Client_ReceivedData(object sender, DataReceivedEventArgs e)
        {
            string data = Encoding.UTF8.GetString(e.Data.Array, 0, e.Data.Count);

            if (data.StartsWith("Exercise"))
            {
                try
                {
                    string[] exerciseParts = data.Split(',');
                    int index = int.Parse(exerciseParts[1]);
                    string equation = exerciseParts[2];
                    string indication = exerciseParts[3];
                    EquationType type = Enum.Parse<EquationType>(exerciseParts[4]);
                    bool showSolve = bool.Parse(exerciseParts[5]);

                    Exercise receivedExercise = new()
                    {
                        Index = index,
                        Equation = equation,
                        SolveCount = 0,
                        Indication = indication,
                        Type = type,
                        SolutionVisible = showSolve
                    };

                    Dispatcher.UIThread.Post(() =>
                        LoadExercise(receivedExercise)
                    );
                }
                catch { }
            }
        }

        private void Server_ReceivedData(object sender, DataReceivedEventArgs e)
        {
            string data = Encoding.UTF8.GetString(e.Data.Array, 0, e.Data.Count);

            if (data.StartsWith("Solve"))
            {
                try
                {
                    string[] solveParts = data.Split(',');
                    int index = int.Parse(solveParts[1]);
                    int count = int.Parse(solveParts[2]);
                    string name = solveParts[3];

                    if (index == Exercise.Index)
                    {
                        Solves.Add((name, count));

                        Dispatcher.UIThread.Post(() =>
                            TbFeedback.Text = name + " a rezolvat!\n\n" + Solves.Count + "/" + Server.Connections + " rezolvări până acum:\n" + Solves.Select(x => x.Item1 + " - " + x.Item2.ToString() + (x.Item2 == 1 ? " încercare" : " încercări")).Aggregate((t, x) => t += "\n" + x)
                        );
                    }
                }
                catch { }
            }
            else if (data.StartsWith("Exercise"))
            {
                try
                {
                    string[] exerciseParts = data.Split(',');
                    int index = int.Parse(exerciseParts[1]);
                    string equation = exerciseParts[2];
                    EquationType type = Enum.Parse<EquationType>(exerciseParts[4]);

                    Question = new()
                    {
                        Index = index,
                        Equation = equation,
                        SolveCount = 0,
                        Indication = "Întrebare",
                        Type = type,
                        SolutionVisible = true
                    };

                    Dispatcher.UIThread.Post(() =>
                        {
                            FormulaQuestion.Formula = ((Entity)Question.Equation).Latexise();
                            QuestionPanel.IsVisible = true;
                            QuestionPanel.Opacity = 1;
                        }
                    );
                }
                catch { }
            }
        }

        private async void Server_ClientConnected(object sender, ConnectionEventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
                TbFeedback.Text = "Elev conectat.\nElevi: " + Server.Connections
            );

            if (Exercise != null)
                await Server.SendAsync(e.IpPort, $"Exercise,{Exercise.Index},{Exercise.Equation},{Exercise.Indication},{Exercise.Type},{Exercise.SolutionVisible}");
        }

        private void Client_Connected(object sender, ConnectionEventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
                {
                    BtConnect.IsEnabled = false;
                }
            );
        }

        private void Client_Disconnected(object sender, ConnectionEventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
                {
                    BtConnect.IsEnabled = true;
                }
            );
        }
    }

    public class Exercise
    {
        public string Equation = "7 + 2 * x + ln(x) + (x ^ 2)";
        public int Index = 0;
        public string Indication = "";
        public int SolveCount = 0;
        public EquationType Type = EquationType.Calcul;

        public bool SolutionVisible = true;
        public bool Solved = false;
    }

    public enum EquationType
    {
        Calcul,
        Ecuatie,
        Derivare,
        Integrare
    }
}