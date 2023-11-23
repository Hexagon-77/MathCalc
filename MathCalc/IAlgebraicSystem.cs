using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MathCalc
{
    public interface IAlgebraicEntity
    {
        public string GetLatex();

        public IAlgebraicEntity Solve();
        public IAlgebraicEntity Calculate();
        public IAlgebraicEntity Differentiate();
        public IAlgebraicEntity Integrate();
    }

    public class AngouriEntity : IAlgebraicEntity
    {
        public AngouriMath.Entity Entity;

        public AngouriEntity() { }

        public AngouriEntity(AngouriMath.Entity entity)
        {
            Entity = entity;
        }

        public string GetLatex()
            => Entity.Latexise();

        public static AngouriEntity ParseString(string value)
            => new(value.Replace("lim", "limit").Replace("deriv(", "derivative(").Replace("int(", "integral(").Replace("tg", "tan").Replace("+inf", "+oo").Replace("-inf", "-oo").Replace("inf", "+oo"));

        public static bool Compare(IAlgebraicEntity a, IAlgebraicEntity b)
        {
            AngouriEntity A = a as AngouriEntity, B = b as AngouriEntity;

            if (A.Entity.Simplify().EqualsImprecisely(B.Entity.Simplify()))
                return true;

            if (A.Entity is AngouriMath.Entity.Set.FiniteSet set && set.Count == 1 && set.First().Simplify().EqualsImprecisely(B.Entity.Simplify()))
                return true;

            return false;
        }

        public IAlgebraicEntity Solve()
            => new AngouriEntity(Entity.Solve("x"));

        public IAlgebraicEntity Calculate()
            => new AngouriEntity(Entity.Simplify());

        public IAlgebraicEntity Differentiate()
            => new AngouriEntity(Entity.Differentiate("x").Simplify());

        public IAlgebraicEntity Integrate()
            => new AngouriEntity(Entity.Integrate("x").Simplify());
    }
}
