using System;

namespace CCIUtilities
{
    /// <summary>
    /// Implements fitting a sphere to a set of electrode locations
    /// </summary>
    public class SphereFit
    {
        double _R;
        /// <summary>
        /// Fitted spherical radius
        /// </summary>
        public double R { get { return _R; } }
        double _x0;
        /// <summary>
        /// X displacement of the fitted sphere origin
        /// </summary>
        public double X0 { get { return _x0; } }
        double _y0;
        /// <summary>
        /// Y displacement of the fitted sphere origin
        /// </summary>
        public double Y0 { get { return _y0; } }
        double _z0;
        /// <summary>
        /// Z displacement of the fitted sphere origin
        /// </summary>
        public double Z0 { get { return _z0; } }
        double _eta;
        /// <summary>
        /// Eigenvalue with smallest magnitude; this is the one used to calculate the spherical parameters
        /// </summary>
        public double Eta { get { return _eta; } }

        /// <summary>
        /// Constructs a spherical fit to points expressed in cartesian coordinates using Taubin algebraic fit
        /// After construction, access properties R for radius and X0, Y0, and Z0 for center of sphere
        /// </summary>
        /// <param name="XYZ">[N, 3] array of points to fit</param>
        /// <see cref="https://arxiv.org/pdf/0907.0421.pdf"/>
        public SphereFit(double[,] XYZ)
        {
            int N = XYZ.GetLength(0);
            if (N < 4) throw new ArgumentException("In SphereFit.cotr: too few input points");

            //Calculate estimated moments
            double qm = 0, xm = 0, ym = 0, zm = 0; //mean of radius^2, x, y, z
            double[,] VV = new double[4, 4]; //Cross moments, including radius^2
            for (int i = 0; i < N; i++)
            {
                double X = XYZ[i, 0];
                double Y = XYZ[i, 1];
                double Z = XYZ[i, 2];
                double Q = X * X + Y * Y + Z * Z;
                qm += Q;
                xm += X;
                ym += Y;
                zm += Z;
                double[] V = new double[] { Q, X, Y, Z };
                for (int k = 0; k < 4; k++)
                    for (int l = k; l < 4; l++)
                        VV[k, l] += V[k] * V[l];
            }
            double NN = (double)N;
            qm /= NN;
            xm /= NN;
            ym /= NN;
            zm /= NN;
            double qqm = VV[0, 0] / NN;
            double qxm = VV[0, 1] / NN;
            double qym = VV[0, 2] / NN;
            double qzm = VV[0, 3] / NN;
            double xxm = VV[1, 1] / NN;
            double xym = VV[1, 2] / NN;
            double xzm = VV[1, 3] / NN;
            double yym = VV[2, 2] / NN;
            double yzm = VV[2, 3] / NN;
            double zzm = VV[3, 3] / NN;

            double sq = Math.Sqrt(1D - 8D * qm + 16D * (xm * xm + ym * ym + zm * zm + qm * qm));
            double t = 1D / Math.Sqrt(0.5 * (1D + 4D * qm - sq));
            const double v = 1000D;
            NMMatrix PhiB = new NMMatrix(new double[,] {
            { 0, -zm/xm, 0, t, 0 },
            { 0, -ym/xm, v, 0, 0 },
            { 0, 0, 0, 0, 1D / Math.Sqrt(0.5 * (1D + 4D * qm + sq)) },
            { (4D * qm - 1D - sq) / (4D * zm), xm / zm, v * ym / zm, t, 0 },
            { (4D * qm - 1D + sq) / (4D * zm), xm / zm, v * ym / zm, t, 0  }
            });

            NMMatrix M = new NMMatrix(new double[,] {
            {qqm, qxm, qym, qzm, qm},
            {qxm, xxm, xym, xzm, xm},
            {qym, xym, yym, yzm, ym},
            {qzm, xzm, yzm, zzm, zm},
            {qm, xm, ym, zm, 1D}});

            NMMatrix AA = PhiB.Transpose() * M * PhiB; //symmetric 5 x 5 matrix
            NMMatrix.Eigenvalues eigen = new NMMatrix.Eigenvalues(AA);

            //find the smallest positive eigenvalue; there should
            // be at least one positive value
            NVector eta = eigen.e;
            double min = double.MaxValue;
            int index = -1;
            for (int i = 0; i < eta.N; i++)
            {
                t = eta[i];
                if (t >= 0 && t < min) { min = t; index = i; }
            }
            if (index == -1) //no positive eigenvalues
                throw new Exception("In SphereFit.cotr: no positive eigenvalues found");
            _eta = eta[index]; //selected eigenvalue

            NVector parms = PhiB * eigen.E.ExtractColumn(index); //using selected eigenvector
            double A = 2D * parms[0];
            double B = parms[1];
            double C = parms[2];
            double D = parms[3];
            double E = parms[4];

            _R = Math.Sqrt((B * B + C * C + D * D - 2D * A * E) / (A * A));
            _x0 = -B / A;
            _y0 = -C / A;
            _z0 = -D / A;
        }

        /// <summary>
        /// Fit to sphere using Levenberg-Marquardt algorithm; least square error
        /// </summary>
        /// <param name="XYZ">Data to fit</param>
        /// <param name="scale">Scale to use = approx radius of head in appropriate units; = initial estimate of radius</param>
        /// <param name="sq">Use difference in square distances for error if true, linear distances if false</param>
        public SphereFit(double[,] XYZ, double scale, bool sq = true)
        {
            int N = XYZ.GetLength(0);
            NMMatrix t = new NMMatrix(XYZ);
            NVector y = new NVector(N); //zero vector
            for(int i=0;i<N;i++)
            {
                for (int j = 0; j < 3; j++)
                    t[i, j] = XYZ[i, j];
            }
            NVector pmin = new NVector(new double[] { 0D, -10D * scale, -10D * scale, -10D * scale });
            NVector pmax = new NVector(new double[] { 200D * scale, 10D * scale, 10D * scale, 10D * scale });
            double[] eps = new double[] { 0.00001 * scale, 0.00001 * scale, 0.00001 * scale, 0.00001 * scale };
            LevenbergMarquardt LM;
            if (sq)
                LM = new LevenbergMarquardt(calcY2, calcJ2, pmin, pmax, null, eps, LevenbergMarquardt.UpdateType.Marquardt);
            else
                LM= new LevenbergMarquardt(calcY1, calcJ1, pmin, pmax, null, eps, LevenbergMarquardt.UpdateType.Marquardt);

            NVector pinit = new NVector(new double[] { scale, 0D, 0D, 0D });
            NVector p = LM.Calculate(pinit, t, y);
            _R = p[0];
            _x0 = p[1];
            _y0 = p[2];
            _z0 = p[3];
            _eta = LM.ChiSquare;
        }

        NVector calcY1(NMMatrix t, NVector p)
        {
            double r = p[0];
            double x0 = p[1];
            double y0 = p[2];
            double z0 = p[3];
            NVector y = new NVector(t.N);
            for (int i = 0; i < t.N; i++)
            {
                double dx = t[i, 0] - x0;
                double dy = t[i, 1] - y0;
                double dz = t[i, 2] - z0;
                y[i] = r - Math.Sqrt(dx * dx + dy * dy + dz * dz);
            }
            return y;
        }

        NMMatrix calcJ1(NMMatrix t, NVector p)
        {
            double x0 = p[1];
            double y0 = p[2];
            double z0 = p[3];
            NMMatrix J = new NMMatrix(t.N, p.N);
            for (int i = 0; i < t.N; i++)
            {
                double dx = t[i, 0] - x0;
                double dy = t[i, 1] - y0;
                double dz = t[i, 2] - z0;
                double dr = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                J[i, 0] = 1D;
                J[i, 1] = dx / dr;
                J[i, 2] = dy / dr;
                J[i, 3] = dz / dr;
            }
            return J;
        }

        NVector calcY2(NMMatrix t, NVector p)
        {
            double r = p[0];
            double x0 = p[1];
            double y0 = p[2];
            double z0 = p[3];
            NVector y = new NVector(t.N);
            for(int i = 0; i < t.N; i++)
            {
                double dx = t[i, 0] - x0;
                double dy = t[i, 1] - y0;
                double dz = t[i, 2] - z0;
                y[i] = r * r - dx * dx - dy * dy - dz * dz;
            }
            return y;
        }

        NMMatrix calcJ2(NMMatrix t, NVector p)
        {
            double r = 2D * p[0];
            double x0 = p[1];
            double y0 = p[2];
            double z0 = p[3];
            NMMatrix J = new NMMatrix(t.N, p.N);
            for (int i = 0; i < t.N; i++)
            {
                J[i, 0] = r;
                J[i, 1] = 2D * (t[i, 0] - x0);
                J[i, 2] = 2D * (t[i, 1] - y0);
                J[i, 3] = 2D * (t[i, 2] - z0);
            }
            return J;
        }
    }
}
