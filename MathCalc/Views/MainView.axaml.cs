using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Net;
using System;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;
using Avalonia.Controls.ApplicationLifetimes;
using ReactiveUI;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AngouriMath;
using SuperSimpleTcp;
using AngouriMath;
using AngouriMath.Extensions;

namespace MathCalc.Views
{
    public partial class MainView : UserControl
    {
        Exercise Exercise;

        public static SimpleTcpServer Server;
        public static SimpleTcpClient Client;

        public MainView()
        {
            InitializeComponent();

            CbType.ItemsSource = Enum.GetValues(typeof(EquationType));
            CbType.SelectedIndex = 0;
        }

        private void Calc_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TbFeedback.Text = "";
                Entity expression = TbEquation.Text ?? "0";
                Formula.Formula = expression.Latexise();
                Entity answer;

                switch ((EquationType)(CbType.SelectedItem ?? EquationType.Calcul))
                {
                    default:
                    case EquationType.Calcul:
                        answer = expression.Simplify();
                        break;
                    case EquationType.Ecuatie:
                        answer = expression.Solve("x");
                        break;
                    case EquationType.Derivare:
                        answer = expression.Differentiate("x");
                        break;
                    case EquationType.Integrare:
                        answer = expression.Integrate("x");
                        break;
                    case EquationType.LimitaInfinit:
                        answer = expression.Limit("x", "+oo");
                        break;
                    case EquationType.LimitaMinInfinit:
                        answer = expression.Limit("x", "-oo");
                        break;
                }

                Entity response = TbAnswer.Text ?? "0";

                if (Exercise != null)
                    Exercise.SolveCount++;

                bool correct = response.Simplify().EqualsImprecisely(answer.Simplify());

                // Check user answer
                if (!correct)
                {
                    TbFeedback.Text = "Răspuns corect: " + answer.ToString();
                    TbFeedback.Text += "\nAi răspuns greșit.";
                }
                else
                {
                    TbFeedback.Text = "Ai răspuns correct!";
                }

                if (Exercise != null)
                    TbFeedback.Text += "\n" + Exercise.SolveCount + " încercări până acum.";
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
                Entity expression = TbEquation.Text;
                Formula.Formula = expression.Latexise();
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

            if (Client != null)
            {
                TbEquation.IsVisible = false;
                TbEquation.IsReadOnly = true;
            }
            else
            {
                TbEquation.IsVisible = true;
                TbEquation.IsReadOnly = false;
            }

            TbFeedback.Text = Exercise.Indication;
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Client = new(TbEquation.Text + ":7182");
                Client.Events.DataReceived += Client_ReceivedData;

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
            LoadExercise(new());
            TbIndication.IsVisible = true;
            CbType.IsVisible = true;

            Server = new("127.0.0.1:7182");
            await Server.StartAsync();
            TbFeedback.Text = "Server profesor pornit.\nCod de conectare: " + GetIPAddress().ToString();
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
            try
            {
                Exercise.Equation = TbEquation.Text;
                Exercise.Indication = TbIndication.Text;
                Exercise.Type = (EquationType)CbType.SelectedItem;
                Exercise.Index++;
                LoadExercise();

                foreach (var item in Server.GetClients())
                {
                    await Server.SendAsync(item, $"Exercise,{Exercise.Index},{Exercise.Equation},{Exercise.Indication},{Exercise.Type}");
                };
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

                    Exercise receivedExercise = new()
                    {
                        Index = index,
                        Equation = equation,
                        SolveCount = 0,
                        Indication = indication
                    };

                    Dispatcher.UIThread.Post(() =>
                        LoadExercise(receivedExercise)
                    );
                }
                catch { }
            }
        }
    }

    public class Exercise
    {
        public string Equation = "7 + 2 * x + ln(x) + (x ^ 2)";
        public int Index = 0;
        public string Indication = "";
        public int SolveCount = 0;
        public EquationType Type = EquationType.Calcul;
    }

    public enum EquationType
    {
        Calcul,
        Ecuatie,
        Derivare,
        Integrare,
        LimitaInfinit,
        LimitaMinInfinit
    }
}