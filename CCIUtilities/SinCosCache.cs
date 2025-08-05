using System;

namespace CCIUtilities
{
    /// <summary>
    /// Calculates Sin and Cos of n * Angle raised to the p power and caches it in Vs or Vc[n, p] for subsequent
    /// lookup if asked for again; when Angle changes, invalidates the cache.
    /// </summary>
    public class SinCosCache
    {
        double _t = double.NaN;
        /// <summary>
        /// Angle for which the current cache is valid
        /// </summary>
        public double Angle
        {
            get { return _t; }
            set
            {
                Reset(value); //setting new value resets cache
            }
        }

        double[,] Vs;
        double[,] Vc;
        int _vLength;

        /// <summary>
        /// Initializes cache
        /// </summary>
        /// <param name="angle">Angle for which the cache will be valid</param>
        /// <param name="size">Maximum valid n & p value</param>
        public SinCosCache(double angle, int size = 20)
        {
            _vLength = size;
            Vs = new double[size, size];
            Vc = new double[size, size];
            Reset(angle);
        }

        public SinCosCache(int size = 20)
        {
            _vLength = size;
            Vs = new double[size, size];
            Vc = new double[size, size];
        }

        /// <summary>
        /// Calculate or look-up Pow(Sin(n * Angle), p); handles negative values of n properly
        /// </summary>
        /// <param name="n">n</param>
        /// <param name="p">p</param>
        /// <returns></returns>
        public unsafe double Sin(int n = 1, int p = 1)
        {
            int n1 = n < 0 ? -n : n;
            fixed (double* ptr = &Vs[n1 - 1, p - 1])
            {
                if (double.IsNaN(*ptr)) //then value has not yet been calculated
                {
                    if (p > 1)
                    {
                        double* ptr1 = ptr - (p - 1);
                        if (double.IsNaN(*ptr1))
                            *ptr1 = Math.Sin(n1 * _t);
                        *ptr = Math.Pow(*ptr1, p);
                    }
                    else
                        *ptr = Math.Sin(n1 * _t);
                }
                return n1 == n ? *ptr : -*ptr;
            }
        }

        /// <summary>
        /// Calculate or look-up Pow(Cos(n * Angle), p); handles negative values of n properly
        /// </summary>
        /// <param name="n"></param>
        /// <param name="p"></param>
        /// <returns></returns>
        public unsafe double Cos(int n = 1, int p = 1)
        {
            n = n < 0 ? -n : n;
            fixed (double* ptr = &Vc[n - 1, p - 1])
            {
                if (double.IsNaN(*ptr)) //then value has not yet been calculated
                {
                    if (p > 1)
                    {
                        double* ptr1 = ptr - (p - 1);
                        if (double.IsNaN(*ptr1))
                            *ptr1 = Math.Cos(n * _t);
                        *ptr = Math.Pow(*ptr1, p);
                    }
                    else
                        *ptr = Math.Cos(n * _t);
                }
                return *ptr;
            }
        }

        public double Tan(int n = 1, int p = 1)
        {
            return Sin(n, p) / Cos(n, p);
        }

        public double Cot(int n = 1, int p = 1)
        {
            return Cos(n, p) / Sin(n, p);
        }

        public double Sec(int n = 1, int p = 1)
        {
            return 1D / Cos(n, p);
        }

        public double Csc(int n = 1, int p = 1)
        {
            return 1D / Sin(n, p);
        }

        /// <summary>
        /// Clear the cache and change valid Angle to angle
        /// </summary>
        /// <param name="angle">New valid Angle</param>
        public void Reset(double angle)
        {
            if (_t == angle) return; //ignore reset if angle is unchanged
            _t = angle;
            for (int i = 0; i < _vLength; i++)
                for (int j = 0; j < _vLength; j++)
                {
                    Vs[i, j] = double.NaN;
                    Vc[i, j] = double.NaN;
                }
        }
    }
}
