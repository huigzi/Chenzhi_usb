using MathNet.Numerics.Data.Matlab;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics.LinearAlgebra;

namespace UsbTest
{
    public class Algorithm
    {

        private double[] rangeBuf = new double[5];

        public double Process()
        {

            const double thd = 50;

            var bp = MatlabReader.ReadAll<double>("bp.mat")["bp"].Row(0);

            var bl = MatlabReader.ReadAll<double>("bl.mat")["bl"].Row(0);

            var signal = MatlabReader.ReadAll<double>("signal.mat")["s"];

            var zeros = Matrix<double>.Build.Dense(41, signal.ColumnCount, 0);

            var sp = Matrix<double>.Build.Dense(signal.RowCount, signal.ColumnCount);

            var env = Matrix<double>.Build.Dense(signal.RowCount, signal.ColumnCount);

            var temp1 = zeros.Stack(signal);

            for (int i = 0; i < 5; i++)
            {
                var temp2 = temp1.Column(i);

                for (int j = 0; j < 521; j++)
                {
                    var temp3 = temp2.SubVector(j, bp.Count);
                    var temp4 = temp3.PointwiseMultiply(bp).Sum();
                    sp[j, i] = temp4;
                }
            }

            var zeros2 = Matrix<double>.Build.Dense(128, signal.ColumnCount, 0);

            var temp5 = zeros2.Stack(sp.PointwiseAbs());

            for (int i = 0; i < 5; i++)
            {
                var temp2 = temp5.Column(i);

                for (int j = 0; j < 521; j++)
                {
                    var temp3 = temp2.SubVector(j, bl.Count);
                    var temp4 = temp3.PointwiseMultiply(bl).Sum();
                    env[j, i] = temp4;
                }
            }


            int[] peak = new int[signal.ColumnCount];
            int[] detector = new int[signal.ColumnCount];

            for (int i = 0; i < 5; i++)
            {
                var temp2 = env.Column(i);
                var temp3 = temp2.MaximumIndex() + 1;

                if (temp2[temp3] > thd)
                {
                    peak[i] = temp3;
                    detector[i] = 1;
                }
            }

            int r = 0;

            if (detector.Sum() >= 3)
            {
                r = peak.Sum();
            }

            var range = ((double)r / detector.Sum() + 200 - 20 - 64) * 344 / 2 / 180000;

            rangeBuf[4] = rangeBuf[3];

            return range;


            //double[,] signal = new double[720, 6];

            //for (int i = 0; i < 720; i++)
            //{
            //    for (int j = 0; j < 6; j++)
            //    {
            //        signal[i, j] = data[720 * i + 2 + j];
            //    }
            //}
        }
    }
}
