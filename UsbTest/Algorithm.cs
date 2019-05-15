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

        private double[] rangeMidBuf = new double[5];
        private readonly Queue<double> rangeBuf = new Queue<double>(64);

        private readonly double winlen = 50;
        private readonly double overlap = 35;

        private readonly double triggerBegin = 2.5;
        private readonly double triggerPlayBegin = 1.5;
        private readonly double triggerPlayStop = 2;
        private readonly double triggerVlmBegin = 2;

        private readonly Vector<double> _bp = MatlabReader.ReadAll<double>("bp.mat")["bp"].Row(0);
        private readonly Vector<double> _bl = MatlabReader.ReadAll<double>("bl.mat")["bl"].Row(0);

        private int gestureStartFlag = 0;
        private int volumeStartFlag = 0;
        private int playStartPauseFlag = 0;

        public int Volume { get; private set; } = 50;
        public bool StartFlag { get; private set; } = false;


        public void GestureCalculate(Matrix<double> signal)
        {
            const double thd = 50;

            var zeros = Matrix<double>.Build.Dense(41, signal.ColumnCount, 0);

            var sp = Matrix<double>.Build.Dense(signal.RowCount, signal.ColumnCount);

            var env = Matrix<double>.Build.Dense(signal.RowCount, signal.ColumnCount);

            var temp1 = zeros.Stack(signal);

            for (int i = 0; i < 5; i++)
            {
                var temp2 = temp1.Column(i);

                for (int j = 0; j < 520; j++)
                {
                    var temp3 = temp2.SubVector(j, _bp.Count);
                    var temp4 = temp3.PointwiseMultiply(_bp).Sum();
                    sp[j, i] = temp4;
                }
            }

            var zeros2 = Matrix<double>.Build.Dense(128, signal.ColumnCount, 0);

            var temp5 = zeros2.Stack(sp.PointwiseAbs());

            for (int i = 0; i < 5; i++)
            {
                var temp2 = temp5.Column(i);

                for (int j = 0; j < 520; j++)
                {
                    var temp3 = temp2.SubVector(j, _bl.Count);
                    var temp4 = temp3.PointwiseMultiply(_bl).Sum();
                    env[j, i] = temp4;
                }
            }

            int[] peak = new int[signal.ColumnCount];
            int[] detector = new int[signal.ColumnCount];

            for (int i = 0; i < 5; i++)
            {
                var temp2 = env.Column(i);
                var temp3 = temp2.MaximumIndex();

                if (temp2[temp3] > thd)
                {
                    peak[i] = temp3;
                    detector[i] = 1;
                }

                temp3 += 1;
            }

            double r = 0;

            if (detector.Sum() >= 1)
            {
                r = peak.Sum();
            }
            else
            {
                return;
            }

            var rangeTemp = (r / detector.Sum() + 200 - 20 - 64) * 344 / 2 / 180000 * 100;

            if(rangeTemp < 10)
            {
                rangeTemp = 60;
            }

            rangeMidBuf[4] = rangeMidBuf[3];
            rangeMidBuf[3] = rangeMidBuf[2];
            rangeMidBuf[2] = rangeMidBuf[1];
            rangeMidBuf[1] = rangeMidBuf[0];
            rangeMidBuf[0] = rangeTemp;

            var range = rangeMidBuf.OrderBy(x => x).ToArray();

            if(range[2] == 0)
            {
                rangeBuf.Enqueue(rangeTemp);
            }
            else
            {
                rangeBuf.Enqueue(range[2]);
            }

            //当数据存储到一定个数时

            if (rangeBuf.Count >= winlen)
            {
                var mean = rangeBuf.Sum() / rangeBuf.Count;
                var stdValue = Math.Sqrt(rangeBuf.Select(x => (x - mean) * (x - mean)).Sum() / (rangeBuf.Count - 1));

                ///用于判断是否有手势需要响应
                if (gestureStartFlag == 0)
                {
                    ///判断手势起始帧
                    if (stdValue < triggerBegin && rangeTemp != 60)
                    {
                        gestureStartFlag = 1;
                    }
                }
                else
                {
                    ///此时手势起始帧已存在
                    ///
                    /// 
                    if(rangeTemp == 60)
                    {
                        return;
                    }

                    ///
                    ///之前已经在做Start手势
                    if(playStartPauseFlag == 1)
                    {
                        ///判断此帧是否为手势控制停止
                        if(stdValue < triggerPlayStop && mean > 45)
                        {
                            playStartPauseFlag = 0;
                            volumeStartFlag = 0;
                            StartFlag = false;

                        }
                    }

                    ///此时手势起始帧已经存在，但是，没有判断出那种手势
                    ///后续代码用于判断手势种类
                    ///
                    /// 
                    ///是否为播放手势
                    if(stdValue < triggerPlayBegin && mean < 25)
                    {
                        playStartPauseFlag = 1;
                        volumeStartFlag = 1;
                        StartFlag = true;
                    }

                    ///是否为音量控制手势
                    ///
                    var temp6 = rangeBuf.Dequeue();

                    if (volumeStartFlag == 1)
                    {
                        if (Math.Abs(temp6 - range[2]) > triggerVlmBegin && rangeTemp != 60)
                        {
                            Volume = (int)(Volume - (temp6 - range[2]) / temp6 * 50);
                            if (Volume > 100)
                            {
                                Volume = 100;
                            }
                            if (Volume < 0)
                            {
                                Volume = 0;
                            }
                        }
                    }
                }

                for (int i = 0; i < winlen - overlap - 1; i++)
                {
                    rangeBuf.Dequeue();
                }
            }
        }
    }
}
