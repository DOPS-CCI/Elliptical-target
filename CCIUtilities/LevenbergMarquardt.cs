using System;

namespace CCIUtilities
{

    public class LevenbergMarquardt
    {
        /// <summary>
        /// Delegate function for calculating value of current fitting function
        /// </summary>
        /// <param name="t">Input points</param>
        /// <param name="p">Paramter values</param>
        /// <returns></returns>
        public delegate NVector Function(NMMatrix t, NVector p);

        /// <summary>
        /// Delegate function for calculating current Jacobian matrix
        /// </summary>
        /// <param name="t">Input points</param>
        /// <param name="p">Parameter values</param>
        /// <returns></returns>
        public delegate NMMatrix JFunc(NMMatrix t, NVector p);

        const double lambda_DN_fac = 2D;
        const double lambda_UP_fac = 3D;
        Function func; //Function to fit data to
        JFunc Jfunc; //Jacobian of the function; if null, then Jacobian is estimated
        NVector p; //Paramter vector m x 1
        NMMatrix t; //Location of known points in space in which function is defined n x d matrix
        NVector y_dat; //Corresponding value to those points  n x 1
        NVector dp; //Delta paramter vector used to estimate Jacobian m x 1
        NVector p_min; //Parameter absolute minima m x 1
        NVector p_max; //Parameter absolute maxima m x 1
        double[] eps; //Halting criteria m x 1
        UpdateType updateType; //Update apporach: Marquardt, Quadratic, Nielsen
        int MaxIter;
        int n; //number of d-dimensioned independent data points
        int m; //number of parameters in fitting function
        int d; // number of dimensions in independent data points

        NMMatrix J; //the current Jacobian n x m
        NMMatrix JtJ; //J-transponse * J m x m matrix
        NVector Jtdy;
        double DOF;
        double lambda;
        double X2;
        double dX2;
        NVector p_old;
        NVector y_old;
        NVector y_hat;
        double alpha;
        double nu;
        int iteration;

        /// <summary>
        /// Criterium used for halting: > 0 is normal result
        /// </summary>
        int _result = 0;
        public ResultType Result
        {
            get { return (ResultType)_result; }
        }

        /// <summary>
        /// Number of iterations required to end calculations
        /// </summary>
        public int Iterations
        {
            get { return iteration; }
        }

        /// <summary>
        /// Chi square value fo the result
        /// </summary>
        public double ChiSquare
        {
            get { return X2 / DOF; }
        }

        /// <summary>
        /// Goodness of fit
        /// </summary>
        public double normalizedStandardErrorOfFit
        {
            get
            {
                return (X2 / DOF - DOF) / Math.Sqrt(2 * DOF);
            }
        }

        /// <summary>
        /// Estimated parameter covariance matrix m x m
        /// </summary>
        public NMMatrix parameterCovariance
        {
            get
            {
                NMMatrix Vp = NMMatrix.I(m) / JtJ;
                return Vp;
            }
        }

        /// <summary>
        /// Estimated standard error of resulting parameters
        /// </summary>
        public NVector parameterStandardError
        {
            get
            {
                NVector Sp = (DOF * (NMMatrix.I(m) / JtJ).Diag()).Apply((NMMatrix.F)Math.Sqrt);
                return Sp;
            }
        }

        /// <summary>
        /// Create Levenberg-Marquardt engine for estimated parameters of non-linear function
        /// </summary>
        /// <param name="func">"Error" function; square of this is minimized</param>
        /// <param name="Jfunc">Jacobian of func; if null this is estimated</param>
        /// <param name="p_min">Minium permissible values of parameters</param>
        /// <param name="p_max">Maximum permissible values of paramters</param>
        /// <param name="dp">Delta of parameters used to esitmate Jacobian; may be null if JFunc is null</param>
        /// <param name="eps">Goal epsilon of the parameters</param>
        /// <param name="updateType">Type of update to be performed</param>
        public LevenbergMarquardt(Function func, JFunc Jfunc, NVector p_min, NVector p_max, NVector dp, double[] eps, UpdateType updateType)
        {
            this.func = func;
            this.Jfunc = Jfunc;
            m = p_min.N; //number of parameters to calculate
            this.p_min = p_min;
            if (p_max.N != m) throw new Exception("LevenbergMarquardt: size mismatch p_max");
            this.p_max = p_max;
            if (Jfunc == null)
            {
                if (dp.N != m) throw new Exception("LevenbergMarquardt: size mismatch dp");
                this.dp = dp;
            }
            MaxIter = 50 * m;
            this.eps = eps;
            this.updateType = updateType;
        }

        /// <summary>
        /// Calculate least squares fit
        /// </summary>
        /// <param name="par_initial">Initial parameter estimates m x 1 vector</param>
        /// <param name="t">Location of points to fit, a n x d matrix</param>
        /// <param name="y_dat">Data values at these points, a n x 1 vector; = 0 for finding a minimum</param>
        /// <returns>Parameter estimate vector, a m x 1 vector</returns>
        public NVector Calculate(NVector par_initial, NMMatrix t, NVector y_dat)
        {
            _result = 0;
            n = t.N;
            d = t.M;
            if (par_initial.N != m) throw new Exception("LevenbergMarquardt.Calculate: size mismatch parms");
            this.p = par_initial;
            this.t = t;
            if (y_dat.N != n) throw new Exception("LevenbergMarquardt.Calculate: size mismatch t-y");
            this.y_dat = y_dat;

//            weight_sq = (m - n + 1) / y_dat.Dot(y_dat);
            DOF = (double)(n - m + 1);

            //initalize Jacobian and related matrices
            y_hat = func(t, p);
            y_old = y_hat;
            if (Jfunc == null)
                J = Jacobian(p, y_hat);
            else
                J = Jfunc(t, p);
            NVector delta_y = y_dat - y_hat;
            X2 = delta_y.Dot(delta_y);
            JtJ = J.Transpose() * J;
            Jtdy = J.Transpose() * delta_y;

            iteration = 0;

            if (Jtdy.Abs().Max() < eps[0])
            {
                _result = 1;
                return p; //Good guess!!!
            }
            if (updateType == UpdateType.Marquardt)
                lambda = 0.01D;
            else
                lambda = 0.01D * JtJ.Diag().Max();

            bool stop = false;

            /************************** Begin Main loop ***********************/
            // y_hat = vector of y estimates for current value of parameters
            // y_try = vector of y estimates for current trial value of parameters
            // y_dat = given dependent values (fixed from input)
            // y_old = vector of y estimates for previous value of parameters (used in Broyden estimate of J)
            // t = given independent values (fixed input points)
            // p = current accepted estimate of parameters
            // h = last calculated (trial) increment for the parameters
            // p_try = current trial value for the parameters
            // p_old = previous accepted value of parameters (used in Broyden estimate of J)
            // X2 = chi^2 of last accepted estimate
            // X2_try = chi^2 of current trial estimate
            // J = current estimate of Jacobian at p

            while (!stop)
            {
                iteration++;

                NVector h;
                if (updateType == UpdateType.Marquardt)
                    h = Jtdy / (JtJ + lambda * JtJ.Diag().Diag());
                else
                    h = Jtdy / (JtJ + lambda * NMMatrix.I(m));

                NVector p_try = (p + h).Max(p_min).Min(p_max);

                NVector y_try = func(t, p_try);
                delta_y = y_dat - y_try;

                double X2_try = delta_y.Dot(delta_y);

                if (updateType == UpdateType.Quadratic)
                {
                    alpha = Jtdy.Dot(h) / ((X2_try - X2) / 2D + 2D * Jtdy.Dot(h));
                    h = h * alpha;
                    p_try = (p_try + h).Max(p_min).Min(p_max);
                    delta_y = y_dat - func(t, p_try);
                    X2_try = delta_y .Dot(delta_y);
                }
                dX2 = X2_try - X2;

                double rho = -dX2 / (2D * (lambda * h + Jtdy).Dot(h));

                if (dX2 < 0D) //found a better estimate
                {
                    X2 = X2_try;
                    p_old = p;
                    p = p_try;
                    y_old = y_hat;
                    y_hat = y_try;

                    if (iteration % (2 * m) == 0) //|| dX2 > 0 or is it rho > ep[3] ?
                        if (Jfunc == null)
                            J = Jacobian(p, y_hat);
                        else
                            J = Jfunc(t, p);
                    else
                        J = J + (y_hat - y_old - J * h).Cross(h) / h.Dot(h); //Broyden rank-1 update of J

                    JtJ = J.Transpose() * J;
                    Jtdy = J.Transpose() * delta_y;

                    switch (updateType)
                    {
                        case UpdateType.Marquardt:
                            lambda = Math.Max(lambda / lambda_DN_fac, 1E-7);
                            break;
                        case UpdateType.Quadratic:
                            lambda = Math.Max(lambda / (1 + alpha), 1E-7);
                            break;
                        case UpdateType.Nielsen:
                            lambda = lambda * Math.Max(1D / 3D, 1D - Math.Pow(2D * rho - 1D, 3));
                            nu = 2D;
                            break;
                    }

                    if (Jtdy.Abs().Max() < eps[0] && iteration > 2)
                    {
                        _result = 1;
                        stop = true;
                    }
                    else if ((h / p).Abs().Max() < eps[1] && iteration > 2)
                    {
                        _result = 2;
                        stop = true;
                    }
                    else if (X2 / (n - m + 1) < eps[2] && iteration > 2)
                    {
                        _result = 3;
                        stop = true;
                    }
                }
                else //Not a better estimate
                {
                    if (iteration % (2 * m) == 0) //update J every 2n th no matter what
                    {
                        if (Jfunc == null)
                            J = Jacobian(p, y_hat);
                        else
                            J = Jfunc(t, p);
                        JtJ = J.Transpose() * J;
                        Jtdy = J.Transpose() * (y_dat - y_hat);
                    }

                    switch (updateType)
                    {
                        case UpdateType.Marquardt:
                            lambda = Math.Min(lambda * lambda_UP_fac, 1E7);
                            break;
                        case UpdateType.Quadratic:
                            lambda = lambda + Math.Abs(dX2 / (2D * alpha));
                            break;
                        case UpdateType.Nielsen:
                            lambda = lambda * nu;
                            nu *= 2D;
                            break;
                    }
                }

                if (iteration > MaxIter && !stop)
                {
                    _result = -1;
                    return p;
                }
            }
            /************************** End Main loop ************************/

            return p;
        }

        /// <summary>
        /// Estimate Jacobian when closed form not available
        /// </summary>
        /// <param name="p">Current parameter estimate</param>
        /// <param name="y">Current ouput estimates based on this parameter setting</param>
        /// <returns>Estimated value of Jacobian</returns>
        private NMMatrix Jacobian(NVector p, NVector y)
        {
            NVector ps = new NVector(p); //save a copy
            NMMatrix J = new NMMatrix(n, m); //creating a new J from scratch
            double del_p;
            for (int j = 0; j < m; j++)
            {
                del_p = Math.Max(dp[j] * Math.Abs(p[j]), dp[j]);
                p[j] = ps[j] + del_p;
                NVector y1 = func(t, p);
                if (dp[j] != 0D) //forward or backward difference
                    J.ReplaceColumn(j, (y1 - y) / del_p);
                else //central difference
                {
                    p[j] = ps[j] - del_p;
                    J.ReplaceColumn(j, (y1 - func(t, p)) / (2D * del_p));
                }
                p[j] = ps[j]; //restore this value
            }
            return J;
        }

        private NMMatrix Broyden(NVector p_old, NVector y_old, NMMatrix J, NVector p, NVector y)
        {
            NVector h = p - p_old;
            J = J + (y - y_old - J * h).Cross(h) / h.Dot(h);
            return J;
        }

        public enum UpdateType { Marquardt, Quadratic, Nielsen };

        public enum ResultType { MaximumIterations = -1, NoResult = 0, Jacobian = 1, ParameterChange = 2, ChiSquare = 3 };
    }
}
