using System;

namespace CCIUtilities
{
    public class SphericalHarmonic
    {
        const int fixedOrder = -1; //start with empty cache
        static SH[][] HigherOrder;
        static int maxOrder = fixedOrder;

        /// <summary>
        /// Precalculate SH information
        /// </summary>
        /// <param name="max">Maximum order to precalculate</param>
        /// <remarks>By precalculating the basis of SHs up to some order, one can speed up
        /// calculation of SH values and derivatives. This information is cached and then used
        /// by the static methods Y and DY. This cache can be increased at any time by
        /// recalling this routine with a higher value of max.</remarks>
        public static void CreateSHEngine(int max)
        {
            if (max <= maxOrder) return; //use existng formulas

            SH[][] newHigherOrder = new SH[max - fixedOrder][];
            for (int i = 0; i < maxOrder - fixedOrder; i++)
                newHigherOrder[i] = HigherOrder[i]; //copy existing across
            HigherOrder = newHigherOrder;
            for (int l = maxOrder + 1; l <= max; l++) //add to cache
            {
                HigherOrder[l] = new SH[2 * l + 1];
                for (int m = -l; m <= l; m++)
                    HigherOrder[l][m + l] = new SH(l, m);
            }
            maxOrder = max;
        }

        public static double Y(int l, int m, SinCosCache theta, SinCosCache phi)
        {
            if (l < 0 || l < Math.Abs(m)) return 0D;
            try
            {
                if (l <= maxOrder) //can use precalculated SH information
                    return HigherOrder[l][l + m].EvaluateAt(theta, phi);
                else
                    return (new SH(l, m)).EvaluateAt(theta, phi);
            }
            catch (Exception)
            {
                throw new ArgumentOutOfRangeException("In SphericalHarmonic.Y: Invalid (m, l) = (" + l.ToString("0") + ", " + m.ToString("0") + ")");
            }
        }

        public static double DY(int l, int m, SinCosCache theta, SinCosCache phi, Tuple<int, int> order)
        {
            if (l < 0 || l < Math.Abs(m)) return 0D;
            try
            {
                if (l <= maxOrder)
                    return HigherOrder[l][l + m].EvaluateDAt(theta, phi, order);
                else
                    return (new SH(l, m)).EvaluateDAt(theta, phi, order);
            }
            catch (Exception e)
            {
                throw new Exception("In SphericalHarmonic.DY: " + e.Message);
            }
        }

        public static double Y(int l, int m, double theta, double phi)
        {
            if (l < 0 || l < Math.Abs(m)) return 0D;
            try
            {
                if (l <= maxOrder)
                    return HigherOrder[l][l + m].EvaluateAt(theta, phi);
                else
                    return (new SH(l, m)).EvaluateAt(theta, phi);
            }
            catch (Exception)
            {
                throw new ArgumentOutOfRangeException("In SphericalHarmonic.Y: Invalid (m, l) = (" + l.ToString("0") + ", " + m.ToString("0") + ")");
            }
        }

        public static double DY(int l, int m, double theta, double phi, Tuple<int, int> order)
        {
            if (order.Item2 > 0 && m == 0) return 0D;
            try
            {
                if (l <= maxOrder)
                    return HigherOrder[l][l + m].EvaluateDAt(theta, phi, order);
                else
                    return (new SH(l, m)).EvaluateDAt(theta, phi, order);
            }
            catch (Exception e)
            {
                throw new Exception("In SphericalHarmonic.DY: " + e.Message);
            }
        }

        public static string ToString(int l, int m)
        {
            try
            {
                if (l <= maxOrder) //can use precalculated SH information
                    return HigherOrder[l][l + m].ToString();
                else
                    return (new SH(l, m)).ToString();
            }
            catch (Exception)
            {
                throw new ArgumentOutOfRangeException("In SphericalHarmonic.ToString: Invalid (m, l) = (" + l.ToString("0") + ", " + m.ToString("0") + ")");
            }
        }
        /// <summary>
        /// Change linear i index to (l, m) pair
        /// </summary>
        /// <param name="i">Linear index</param>
        /// <param name="l">l</param>
        /// <param name="m">m</param>
        public static void i2lm(int i, out int l, out int m)
        {
            l = (int)Math.Sqrt(i);
            m = i - l * (l + 1);
        }

        /// <summary>
        /// Change (l, m) pair returning linear index
        /// </summary>
        /// <param name="l">l</param>
        /// <param name="m">m</param>
        /// <returns>Linear index</returns>
        public static int lm2i(int l, int m)
        {
            return l * (l + 1) + m;
        }

        /// <summary>
        /// Internal class which codifies a particular (l, m) real spherical harmonic
        /// </summary>
        class SH
        {
            int _l;
            int _m;

            AssociatedLegendre lp; //associated Legendre polynomial
            double Klm; //constant multiplier; +/-1 to indicate odd/even Abs(m)
            bool? mPosNegZero = null; //true=>Cos, false=>-Sin, null=>1; m>0, m<0, m==0

            /// <summary>
            /// Constructor of a real spherical harmonic; generates items that can be used
            /// to rapidly evaluate the particular SH and its derivatives
            /// </summary>
            /// <param name="l"></param>
            /// <param name="m"></param>
            /// <see cref="https://cs.dartmouth.edu/~wjarosz/publications/dissertation/appendixB.pdf"/>
            internal SH(int l, int m)
            {
                _l = l;
                _m = m;
                int p = Math.Abs(m);
                Klm = OddEven.IsEven(p) ? 1D : -1D;
                double q = 1D;
                for (double qi = l - p + 1; qi <= l + p; qi++) q *= qi;
                Klm *= Math.Sqrt((2D * l + 1D) / (2D * Math.PI * q));
                lp = new AssociatedLegendre(l, p);
                mPosNegZero = m != 0 ? (bool?)(m > 0) : null;
            }

            //Evatuate thata-dependent factor
            double FTheta(SinCosCache theta)
            {

                return Klm * lp.EvaluateAt(theta.Cos());
            }

            //Evaluate phi-dependent factor
            double FPhi(SinCosCache phi)
            {
                if (mPosNegZero.HasValue)
                {
                    if ((bool)mPosNegZero) return phi.Cos(_m);
                    else return -phi.Sin(_m); //triple negtive (Klm, Sin(-m), sign) gives negative result
                }
                return 0.707106781186548D; //Sqrt(0.5) * Sqrt(2) == 1
            }

            //Evaluate SH at (theta, phi)
            internal double EvaluateAt(SinCosCache theta, SinCosCache phi)
            {
                return FTheta(theta) * FPhi(phi);
            }

            //Evaluate SH derivative of order at (theta, phi); this is only valid for total derivative order
            //of less than or equal to 2; throws exception if this is not true
            internal double EvaluateDAt(SinCosCache theta, SinCosCache phi, Tuple<int, int> order)
            {
                Polynomial p;
                double th;
                try
                {
                    switch (order.Item1) //Theta derivatives
                    {
                        case 0: //no theta derivative
                            switch (order.Item2)
                            {
                                case 0:
                                    return EvaluateAt(theta, phi); //no derivative requested
                                case 1: //dPhi only => {0, 1}
                                    if (mPosNegZero.HasValue)
                                    {
                                        if ((bool)mPosNegZero) return -_m * FTheta(theta) * phi.Sin(_m); //m > 0
                                        else return -_m * FTheta(theta) * phi.Cos(_m); //m < 0
                                    }
                                    return 0D; //m == 0
                                case 2: //d2Phi => {0, 2}
                                    if (mPosNegZero.HasValue) return -_m * _m * FTheta(theta) * FPhi(phi); //m != 0
                                    return 0D; //m == 0
                                default:
                                    throw null;
                            }
                        case 1: //first theta derivative
                            p = lp.PolynomialPart; //associated Legendre polynominal
                            th = theta.Cos(); //lookup once
                            double d; //theta portion of derivative
                            if (lp.SQ) //odd m
                                d = Klm * (th * p.EvaluateAt(th) - theta.Sin(1, 2) * p.EvaluateDAt(th));
                            else //even m
                                d = -Klm * theta.Sin() * p.EvaluateDAt(th);
                            switch (order.Item2) //handle possible phi derivative
                            {
                                case 0: //dTheta only => {1, 0}
                                    return d * FPhi(phi);
                                case 1: //dTheta dPhi => {1, 1}
                                    if (mPosNegZero.HasValue) return -d * _m * ((bool)mPosNegZero ? phi.Sin(_m) : phi.Cos(_m));
                                    return 0D;
                                default:
                                    throw null;
                            }
                        case 2: //second theta derivative
                            if (order.Item2 == 0) //d2Theta => {2, 0}
                            {
                                p = lp.PolynomialPart; //associated Legendre polynominal
                                th = theta.Cos(); //lookup once
                                if (lp.SQ) //odd m
                                    return Klm * (-theta.Sin() * (p.EvaluateAt(th)
                                        + 3D * th * p.EvaluateDAt(th))
                                        + theta.Sin(1, 3) * p.EvaluateD2At(th)) * FPhi(phi);
                                else //even m
                                    return Klm * (-th * p.EvaluateDAt(th)
                                        + theta.Sin(1, 2) * p.EvaluateD2At(th)) * FPhi(phi);
                            }
                            else
                                throw null;
                        default:
                            throw null;
                    }
                }
                catch (Exception e)
                {
                    if (e == null)
                        throw new ArgumentException("Unimplemented derivative (theta, phi): (" +
                            order.Item1.ToString("0") + ", " + order.Item2.ToString("0") + ")");
                    else
                        throw e;
                }
            }

            internal new string ToString()
            {
                double klm = Klm;
                string phistring;
                if (mPosNegZero.HasValue)
                {
                    if ((bool)mPosNegZero) phistring = $" * Cos({_m:0}ph)";
                    else phistring = $" * Sin({-_m}ph)";
                }
                else
                {
                    klm *= 0.707106781186548D; //Sqrt(0.5)
                    phistring = "";
                }
                string polystring = (klm * lp.PolynomialPart).ToString();
                if (lp.PolynomialPart.maxPower > 0) polystring = "(" + polystring + ")";
                string sqr = lp.SQ ? " * Sin(th)" : "";
                return $"SH[{_l:0}, {_m:0}] = {polystring.Replace("z", " Cos(th)")}{sqr}{phistring}";
            }

            #region NO CACHING SIN/COS

            double FTheta(double theta)
            {
                return Klm * lp.EvaluateAt(Math.Cos(theta));
            }

            double FPhi(double phi)
            {
                if (mPosNegZero.HasValue)
                {
                    if ((bool)mPosNegZero) return Math.Cos(_m * phi);
                    else return -Math.Sin(_m * phi);
                }
                return 0.707106781186548D;
            }

            //Evaluate SH at (theta, phi)
            internal double EvaluateAt(double theta, double phi)
            {
                return FTheta(theta) * FPhi(phi);
            }

            internal double EvaluateDAt(double theta, double phi, Tuple<int, int> order)
            {
                Polynomial p;
                double th;
                try
                {
                    switch (order.Item1) //Theta derivatives
                    {
                        case 0:
                            switch (order.Item2)
                            {
                                case 0:
                                    return EvaluateAt(theta, phi);
                                case 1: //dPhi
                                    if (mPosNegZero.HasValue)
                                    {
                                        if ((bool)mPosNegZero) return -_m * FTheta(theta) * Math.Sin(_m * phi);
                                        else return -_m * FTheta(theta) * Math.Cos(_m * phi);
                                    }
                                    return 0D;
                                case 2: //d2Phi
                                    if (mPosNegZero.HasValue) return -_m * _m * FTheta(theta) * FPhi(phi);
                                    return 0D;
                                default:
                                    throw null;
                            }
                        case 1:
                            p = lp.PolynomialPart;
                            th = Math.Cos(theta);
                            double d;
                            if (lp.SQ) //odd m
                                d = Klm * (th * p.EvaluateAt(th) - Math.Pow(Math.Sin(theta), 2) * p.EvaluateDAt(th));
                            else //even m
                                d = -Klm * Math.Sin(theta) * p.EvaluateDAt(th);
                            switch (order.Item2)
                            {
                                case 0: //dTheta
                                    return d * FPhi(phi);
                                case 1: //dTheta dPhi
                                    if (mPosNegZero == null) return 0D;
                                    return -d * _m * ((bool)mPosNegZero ? Math.Sin(_m * phi) : Math.Cos(_m * phi));
                                default:
                                    throw null;
                            }
                        case 2:
                            if (order.Item2 == 0) //d2Theta
                            {
                                p = lp.PolynomialPart;
                                th = Math.Cos(theta);
                                double sth = Math.Sin(theta);
                                if (lp.SQ) //odd m
                                    return Klm * (-sth * (p.EvaluateAt(th)
                                        + 3D * th * p.EvaluateDAt(th))
                                        + Math.Pow(sth, 3) * p.EvaluateD2At(th)) * FPhi(phi);
                                else //even m
                                    return Klm * (-th * p.EvaluateDAt(th)
                                        + sth * sth * p.EvaluateD2At(th)) * FPhi(phi);
                            }
                            else
                                throw null;
                        default:
                            throw null;
                    }
                }
                catch (Exception e)
                {
                    if (e == null)
                        throw new ArgumentException("Unimplemented derivative (theta, phi): (" +
                            order.Item1.ToString("0") + ", " + order.Item2.ToString("0") + ")");
                    else
                        throw e;
                }
            }

            #endregion NO CACHING SIN/COS

        }
    }
}
