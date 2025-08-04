using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace CCIUtilities
{
    public class Affine
    {
        NMMatrix _transform; //upper 3 rows of standard affine transform

        /// <summary>
        /// 3x4 matrix representation of the affine transform
        /// </summary>
        public NMMatrix Transform34 { get { return new NMMatrix(_transform); } }

        /// <summary>
        /// 4x4 matrix representation of the affine transform
        /// </summary>
        public NMMatrix Transform44
        {
            get
            {
                NMMatrix t = _transform.Transpose().Augment(new NVector(new double[] { 0, 0, 0, 1 }));
                return t.Transpose();
            }
        }

        /// <summary>
        /// Vector representing the offset of the affine transform
        /// </summary>
        public NVector Offset
        {
            get
            {
                return _transform.ExtractColumn(3);
            }
            set
            {
                for (int i = 0; i < 3; i++)
                    _transform[i, 3] = value[i];
            }
        }

        #region Constructors
        /// <summary>
        /// Create affine transform with default value
        /// </summary>
        public Affine()
        {
            _transform = new NMMatrix(new double[,] { { 1, 0, 0, 0 }, { 0, 1, 0, 0 }, { 0, 0, 1, 0 } });
        }

        /// <summary>
        /// Create affine transform based on array values
        /// </summary>
        /// <param name="transform">3x4 matrix of values</param>
        public Affine(double[,] transform)
        {
            int n = transform.GetLength(0);
            int m = transform.GetLength(1);
            if (n != 3 || m != 4)
                throw new ArgumentException($"In Affine.cotr: invalid transform matrix size ({n:0}x{m:0})");
            _transform = new NMMatrix(transform);
        }

        /// <summary>
        /// Create affine transform from matrix
        /// </summary>
        /// <param name="transform">4x4 or 3x4 matrix for tranform</param>
        public Affine(NMMatrix transform)
        {
            if (transform.M == 4)
                if (transform.N == 3)
                    _transform = new NMMatrix(transform); //make copy
                else if (transform.N == 4)
                    _transform = transform.ExtractMatrixByRows(0, 3); //ignore 4th row
                else
                    throw new ArgumentException($"In Affine.cotr: invalid transform matrix size ({transform.N:0}x{transform.M:0})");
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="t">Affine transform to copy</param>
        public Affine(Affine t) { _transform = new NMMatrix(t._transform); }
        #endregion Constructors

        public Affine Rotate(int axis, double angle)
        {
            if (axis < 0 || axis >= 3)
                throw new ArgumentOutOfRangeException($"In Affine.Rotate: invalid axis number = {axis:0}");
            double c = Math.Cos(angle);
            double s = Math.Sin(angle);
            NMMatrix t = NMMatrix.I(3);
            int i = (axis + 1) % 3;
            int j = (axis + 2) % 3;
            t[i, i] = t[j, j] = c;
            t[i, j] = -s;
            t[j, i] = s;
            _transform = t * _transform; //(3 x 3) * (3 x 4) => (3 x 4)
            return this;
        }

        /// <summary>
        /// Displace origin
        /// </summary>
        /// <param name="x">x displacement</param>
        /// <param name="y">y displacement</param>
        /// <param name="z">z displacement</param>
        /// <returns>new affine transform</returns>
        public Affine Displace(double x, double y, double z)
        {
            _transform[0, 3] += x; //add in displacement
            _transform[1, 3] += y;
            _transform[2, 3] += z;
            return this;
        }

        public Affine Displace(NVector o)
        {
            for (int i = 0; i < 3; i++) _transform[i, 3] += o[i]; //add in displacement
            return this;
        }

        public Affine Scale(double s)
        {
            _transform *= s;
            return this;
        }

        public Affine Scale(NVector s)
        {
            _transform = new NMMatrix(s.ToArray()) * _transform;
            return this;
        }

        public Affine Transform(Affine t)
        {
            _transform = (t.Transform44 * this.Transform44).ExtractMatrixByRows(0, 3);
            return this;
        }

        public Affine Inverse()
        {
            _transform = this.Transform44.Inverse().ExtractMatrixByRows(0, 3);
            return this;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 4; j++)
                    sb.Append($"{_transform[i, j]:0.0000},");
            return sb.Remove(sb.Length - 1, 1).ToString();
        }

        public string ToString(string format)
        {
            return _transform.ToString(format, " ", "{|}");
        }
    }
}
