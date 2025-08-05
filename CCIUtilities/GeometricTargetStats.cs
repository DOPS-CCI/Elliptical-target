using System;

namespace CCIUtilities
{
    /// <summary>
    /// Class for calculating statistics for a polygonal area; class is instantiated with the 
    /// locations of the vertices of the polygon; then CalculateGeometry can be called
    /// repeatedly with the conditional response location; at that point the various 
    /// statistical methods are valid, including Inside, Mean, M2, Variance and CDF;
    /// CDF has an version which can be called with the response location directly
    /// </summary>
    public class GeometricTargetStats
    {
        int _N; //number of vertices
        /// <summary>
        /// Number of vertices in this polygonal target area
        /// </summary>
        public int N { get { return _N; } }

        bool? _inside = null;
        /// <summary>
        /// Was last response inside the target polygon?
        /// </summary>
        public bool? Inside { get { return _inside; } }

        double[] _lastr = null;
        /// <summary>
        /// Last response location for which calculations made
        /// </summary>
        public double[] LastR { get { return _lastr; } }

        internal double[,] _v; //Location of vertices
        internal double[] _s; //Length of side between v[i] and v[i+1]
        double[] d; //diagnonal lengths
        double[] alt; //altitude magnitudes
        int[] IO; //signs of altitudes = in/out of triangle
        double[] S; //subtended central angles
        double[] A; //area of triangles
        double area; //area of polygon

        /// <summary>
        /// Construct outline of polygonal target area
        /// </summary>
        /// <param name="vertices">Consecutive vertices (x, y) of the polygon</param>
        public GeometricTargetStats(double[,] vertices)
        {
            _N = vertices.GetLength(0);
            if (_N < 3 || vertices.GetLength(1) != 2)
                throw new ArgumentOutOfRangeException(
                    $"In GeometricTargetStats.cotr: vertices not array of 2-D points (dimensions = [{_N:0},{vertices.GetLength(1):0}]).");
            _v = new double[_N, 2];
            _s = new double[_N];
            area = 0;
            d = new double[_N];
            alt = new double[_N];
            IO = new int[_N];
            S = new double[_N];
            A = new double[_N];
            for (int i = 0; i < _N; i++)
            {
                int ip = i1(i);
                double x = _v[i, 0] = vertices[i, 0]; //copy vertex locations
                double y = _v[i, 1] = vertices[i, 1];
                double dx = vertices[ip, 0] - x;
                double dy = vertices[ip, 1] - y;
                _s[i] = Math.Sqrt(dx * dx + dy * dy); //calculate length of polygon sides
                area += 0.5D * (x * vertices[ip, 1] - y * vertices[ip, 0]);
            }
            area = Math.Abs(area); //in case vertices are reversed
        }

        /// <summary>
        /// Value of CDF given the last response calculated
        /// </summary>
        /// <param name="rho">Euclidean distance between target and response</param>
        /// <returns></returns>
        public double CDF(double rho)
        {
            if (!_inside.HasValue)
                throw new Exception("In GeometricTargetStats.CDF(double): GeometricTargetStats.CalculateGeometry has not been called.");
            double sum = 0;
            double d2 = d[0];
            for (int i = 0; i < _N; i++)
            {
                double d1 = d2;
                d2 = d[i1(i)];
                double s = _s[i];
                double altitude = alt[i];
                double a = Math.Min(rho, d1);
                double b = Math.Min(rho, d2);
                double theta = 0;
                if (rho <= altitude) theta = S[i];
                else
                {
                    double t = Math.Acos(altitude / rho);
                    if (rho < d1) theta = Math.Sign(d1 * d1 + s * s - d2 * d2) * (Math.Acos(altitude / d1) - t);
                    if (rho < d2) theta += Math.Sign(d2 * d2 + s * s - d1 * d1) * (Math.Acos(altitude / d2) - t);
                }
                sum += IO[i] * (altitude * Math.Sqrt(a * a + b * b - 2D * a * b * Math.Cos(S[i] - theta)) + rho * rho * theta);
            }
            return 0.5D * sum / area;
        }

        /// <summary>
        /// Find conditional value of the CDF for this polygonal target, given response location r
        /// </summary>
        /// <param name="rho">Euclidean distance between target and response</param>
        /// <param name="r">Given response location {x, y}</param>
        /// <returns></returns>
        public double CDF(double rho, double[] r)
        {
            CalculateGeometry(r);
            return CDF(rho);
        }

        /// <summary>
        /// Conditional mean for last calculated response location
        /// </summary>
        public double Mean
        {
            get
            {
                if (!_inside.HasValue)
                    throw new Exception("In GeometricTargetStats.Mean: GeometricTargetStats.CalculateGeometry has not been called.");
                double sum = 0;
                double d2 = d[0];
                for (int i = 0; i < _N; i++)
                {
                    double d1 = d2;
                    d2 = d[i1(i)];
                    double s = _s[i];
                    double dd = d1 + d2;
                    if (s != dd)
                    {
                        double a = A[i];
                        sum += a * (s * dd * (s * s + (d1 - d2) * (d1 - d2)) -
                            8D * a * a * Math.Log((dd - s) / (dd + s))) /
                            (6D * s * s * s);
                    }
                }
                return sum / area;
            }
        }

        /// <summary>
        /// Second (non-central) moment for last calculated response location
        /// </summary>
        public double M2
        {
            get
            {
                if (!_inside.HasValue)
                    throw new Exception("In GeometricTargetStats.M2: GeometricTargetStats.CalculateGeometry has not been called.");
                double sum = 0;
                double d2 = d[0];
                for (int i = 0; i < _N; i++)
                {
                    double d1 = d2;
                    d2 = d[i1(i)];
                    double s = _s[i];
                    sum += A[i] * (3d * (d1 * d1 + d2 * d2) - s * s) / 12D;
                }
                return sum / area;
            }
        }

        /// <summary>
        /// Conditional variance for last calculated response location
        /// </summary>
        public double Variance
        {
            get
            {
                if (!_inside.HasValue)
                    throw new Exception("In GeometricTargetStats.Variance: GeometricTargetStats.CalculateGeometry has not been called.");
                double mean = Mean;
                return M2 - mean * mean;
            }
        }

        /// <summary>
        /// Calculate the geometric factors for a given response
        /// </summary>
        /// <param name="r">Response location (x, y)</param>
        public void CalculateGeometry(double[] r)
        {
            double rx = r[0];
            double ry = r[1];
            for (int i = 0; i < _N; i++)
            {
                int ip = i1(i);
                double v1x = _v[i, 0];
                double v1y = _v[i, 1];
                double v2x = _v[ip, 0];
                double v2y = _v[ip, 1];
                double dsx = _v[ip, 0] - _v[i, 0];
                double dsy = _v[ip, 1] - _v[i, 1];
                double q = (dsx * ry - dsy * rx + v1x * v2y - v2x * v1y) / _s[i]; //"signed" altitude
                A[i] = 0.5D * q * _s[i]; //"signed" area
                alt[i] = Math.Abs(q); //altitude
                IO[i] = Math.Sign(q); //sign
                dsx = rx - v1x;
                dsy = ry - v1y;
                d[i] = Math.Sqrt(dsx * dsx + dsy * dsy); //diagonal side
            }
            double sum = 0;
            for (int i = 0; i < _N; i++)
            {
                double d1 = d[i];
                double d2 = d[i1(i)];
                double si = _s[i];
                //central angle
                if (d1 == 0 || d2 == 0 || si == 0 || Math.Abs(d1 - d2) >= si) S[i] = 0D;
                else S[i] = Math.Acos((d1 * d1 + d2 * d2 - si * si) / (2D * d1 * d2));
                sum += IO[i] * S[i]; //determine "winding" angle
            }
            if (Math.Abs(sum) < 1E-3) _inside = false; //when outside sum close to zero
            else //when magnitude close to 2PI then fully inside; magnitudes betwen 0 and 2PI are on boundary
            {
                _inside = true;
                if (sum < 0) //reverse vertex order
                    for (int i = 0; i < _N; i++)
                    { IO[i] = -IO[i]; A[i] = -A[i]; } //reverse signed quantities
            }
            _lastr = new double[] { rx, ry }; //save last response location
        }

        internal int i1(int i)
        {
            return (i + 1) % _N;
        }
    }
}

