using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace CCIUtilities
{
    public static class NormalDistribution
    {
        readonly static double f = 2D / Math.Sqrt(Math.PI);
        /// <summary>
        /// Error function
        /// </summary>
        /// <param name="x">Parameter</param>
        /// <returns>Error function of x; accuracy of 1E-12; range -1 to +1</returns>
        /// <remarks>Based on NIST function 7.6.2; https://dlmf.nist.gov/7.6 </remarks>
        public static double Erf(double x)
        {
            if (x == 0D) return 0D;
            if (Math.Abs(x) > 5.12D) return Math.Sign(x);
            double x2 = x * x;
            double s = f * Math.Exp(-x2);
            double q;
            double two = 1D;
            double N = 1D;
            double N2 = 1D;
            double z = x;
            double sum = 0D;
            do
            {
                q = s * two * z / N2;
                sum += q;
                two *= 2D;
                z *= x2;
                N += 2D;
                N2 *= N;
            } while (Math.Abs(q) > 1E-12);
            return sum;
        }

        public static double Erfc(double x)
        {
            return 1D - Erf(x);
        }

        readonly static double sr2 = Math.Sqrt(2D);
        readonly static double sr2p = Math.Sqrt(2D * Math.PI);
        public static double CDF(double x, double mean, double sd)
        {
            return (1D + Erf((x - mean) / (sr2 * sd))) / 2D;
        }

        public static double PDF(double x, double mean, double sd)
        {
            double z = (x - mean) / sd;
            return Math.Exp(-z * z / 2D) / (sd * sr2p);
        }
    }
}
