using System;
using System.Linq;
using hds.shared;
namespace hds
{
    /// <summary>Fixture-only1135 raw draw. Never changes a hit, combat roll, callback or another ability.</summary>
    public static class TypedResistanceQa
    {
        public static int? Draw(string dev,string actualDatabase,string enabled,string raw,ushort ability,int minimum,int exclusiveMaximum,Random supplied=null)
        {
            if(supplied!=null)return supplied.Next(minimum,exclusiveMaximum);
            if(dev!="1" || enabled!="1" || actualDatabase==null || !actualDatabase.StartsWith("mxotest_",StringComparison.Ordinal) ||
                actualDatabase.Length<=8 || !actualDatabase.All(c=>char.IsAsciiLetterOrDigit(c)||c=='_') ||
                ability!=1135 || minimum!=50 || exclusiveMaximum!=81 || !int.TryParse(raw,out int value) || value<minimum || value>=exclusiveMaximum)return null;
            return value;
        }
        public static int? Draw(ushort ability,int minimum,int exclusiveMaximum)=>Draw(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),
            Store.config?.dbParams?.DatabaseName,Environment.GetEnvironmentVariable("MXO_TYPED_RESISTANCE_QA"),
            Environment.GetEnvironmentVariable("MXO_TYPED_RESISTANCE_QA_DAMAGE_DRAW"),ability,minimum,exclusiveMaximum);
    }
}
