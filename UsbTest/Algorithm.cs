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
        /// <summary>
        /// 音量调节窗差
        /// </summary>
        private double[] rangeMidResult = new double[5];

        private Tuple<double, double>[] positionBuf = new Tuple<double, double>[3];

        private readonly Queue<double> rangeBuf = new Queue<double>(64);

        private readonly double winlen = 50;
        private readonly double overlap = 35;

        //手势开始判断
        private readonly double triggerBegin = 2.5;

        //开机关机手势
        private readonly double triggerPlayBegin = 1.5;
        private readonly double triggerPlayStop = 2;

        //上一首手势
        private readonly double triggerLastBegin = 1;
        private readonly double triggerLastStop = 1;

        //下一首手势
        private readonly double triggerNextBegin = 1;
        private readonly double triggerNextStop = 1;

        //停止暂停手势
        private readonly double triggerPauseBegin = 1;
        private readonly double triggerPauseStop = 1;
        private readonly double triggerPauseBegin2 = 1;

        //音量上手势
        private readonly double triggerVolumUpBegin = 1;
        private readonly double triggerVolumUpStop = 1;

        //音量下手势
        private readonly double triggerVolumDownBegin = 1;
        private readonly double triggerVolumDownStop = 1;


        private readonly Vector<double> _bp = MatlabReader.ReadAll<double>("C://bp.mat")["bp"].Row(0);
        private readonly Vector<double> _bl = MatlabReader.ReadAll<double>("C://bl.mat")["bl"].Row(0);

        private int gestureStartFlag = 0;
        private int systemOpenFlag = 0;
        private int pauseFlag = 0;
        private int volumeDownFlag = 0;
        private int volumeUpFlag = 0;
        private int lastFlag = 0;
        private int nextFlag = 0;

        public int Volume { get; private set; } = 50;
        public bool StartFlag { get; private set; } = false;
        public string Other { get; private set; } = "无动作";


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
            double[] rangeForSpin;

            if (detector.Sum() >= 6)
            {
                rangeForSpin = peak.Select((x) => (x + 200 - 20 - 64) * 344d / 2 / 180000 * 100).ToArray();
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

            rangeMidResult[4] = rangeMidResult[3];
            rangeMidResult[3] = rangeMidResult[2];
            rangeMidResult[2] = rangeMidResult[1];
            rangeMidResult[1] = rangeMidResult[0];
            rangeMidResult[0] = range[2];

            if (range[2] == 0)
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
                    ///判断关机手势结束
                    if(systemOpenFlag == 1)
                    {
                        if(stdValue < triggerPlayStop && mean > 45)
                        {
                            systemOpenFlag = 0;
                            StartFlag = false;
                            volumeDownFlag = 0;
                            volumeUpFlag = 0;
                            nextFlag = 0;
                            lastFlag = 0;
                            pauseFlag = 0;
                        }
                    }

                    ///判断上一首手势结束
                    if (lastFlag == 1)
                    {
                        if (stdValue < triggerLastStop || rangeTemp == 60)
                        {
                            lastFlag = 0;
                        }
                    }

                    ///判断下一首手势结束
                    if(nextFlag == 1)
                    {
                        if(stdValue < triggerNextStop || rangeTemp == 60)
                        {
                            nextFlag = 0;
                        }
                    }

                    ///判断播放/暂停 手势结束
                    if(pauseFlag == 1)
                    {
                        if(stdValue < triggerPauseStop || rangeTemp == 60)
                        {
                            pauseFlag = 0;
                        }
                    }

                    ///判断增加音量手势结束
                    if(volumeUpFlag == 1)
                    {
                        if(stdValue < triggerVolumUpStop || rangeTemp == 60)
                        {
                            volumeUpFlag = 0;
                        }
                    }

                    ///判断减小音量手势结束
                    if(volumeDownFlag == 1)
                    {
                        if(stdValue < triggerVolumDownStop || rangeTemp == 60)
                        {
                            volumeDownFlag = 0;
                        }
                    }

                    ///此时手势起始帧已经存在，但是，没有判断出那种手势
                    ///后续代码用于判断手势种类
                    ///
                    /// 
                    ///是否为开机手势
                    if(stdValue < triggerPlayBegin && mean < 25)
                    {
                        systemOpenFlag = 1;
                        StartFlag = true;
                    }

                    ///是否为上一首手势
                    if (rangeMidResult[0] - rangeMidResult[2] > triggerLastBegin 
                        && rangeMidResult[2] - rangeMidResult[4] > triggerLastBegin 
                        && rangeTemp != 60)
                    {
                        lastFlag = 1;
                        nextFlag = 0;
                        volumeDownFlag = 0;
                        volumeUpFlag = 0;
                        pauseFlag = 0;
                    }

                    ///是否为下一首的手势
                    if(rangeMidResult[0] - rangeMidResult[2] > triggerNextBegin 
                        && rangeMidResult[2] - rangeMidResult[4] > triggerNextBegin 
                        && rangeTemp != 60)
                    {
                        lastFlag = 0;
                        nextFlag = 1;
                        volumeDownFlag = 0;
                        volumeUpFlag = 0;
                        pauseFlag = 0;
                    }

                    ///是否为播放/暂停的手势
                    if(stdValue > triggerPauseBegin 
                        && rangeTemp > 20 
                        && rangeTemp < 50 
                        && Math.Abs(rangeMidResult[0] - rangeMidResult[2]) < triggerPauseBegin2
                        && Math.Abs(rangeMidResult[4] - rangeMidResult[2]) < triggerPauseBegin2)
                    {
                        lastFlag = 0;
                        nextFlag = 0;
                        volumeUpFlag = 0;
                        volumeDownFlag = 0;
                        pauseFlag = 1;
                    }

                    ///是否为音量调整手势
                    positionBuf[2] = positionBuf[1];
                    positionBuf[1] = positionBuf[0];
                    positionBuf[0] = FindPosition(rangeForSpin);

                    var vector1 = new double[2]
                    {
                        positionBuf[1].Item1 - positionBuf[2].Item1,
                        positionBuf[1].Item2 - positionBuf[2].Item2
                    };

                    var vecotr2 = new double[2]
                    {
                        positionBuf[0].Item1 - positionBuf[1].Item1,
                        positionBuf[0].Item2 - positionBuf[1].Item2
                    };

                    if(positionBuf[0].Item1 != 100 && positionBuf[0].Item2 != 100)
                    {

                        var tempResult = vector1[0] * vecotr2[1] - vector1[1] * vecotr2[0];

                        ///是否为音量放大手势
                        if (tempResult > triggerVolumUpBegin && rangeTemp != 60)
                        {
                            lastFlag = 0;
                            nextFlag = 0;
                            volumeUpFlag = 1;
                            volumeDownFlag = 0;
                            pauseFlag = 0;

                            Volume += (int)(tempResult) * 20;
                        }

                        ///是否为音量所需手势
                        if(tempResult < -1 * triggerVolumDownBegin && rangeTemp != 60)
                        {
                            lastFlag = 0;
                            nextFlag = 0;
                            volumeUpFlag = 0;
                            volumeDownFlag = 1;
                            pauseFlag = 0;

                            Volume += (int)(tempResult) * 20;
                        }
                    }
                    
                }

                for (int i = 0; i < winlen - overlap - 1; i++)
                {
                    rangeBuf.Dequeue();
                }
            }
        }

        private Tuple<double,double> FindPosition(double[] range)
        {
            double[] m = new double[6] { 17.9625, 31.111, 17.9625, -17.9625, -31.111, -17.9625 };
            double[] n = new double[6] { 31.111, 0, -31.111, -31.111, 0, 31.111 };

            var tempRange = range.ToList();

            range.Where((x) => x > 10).Select(x => x).ToArray();

            if(range.Count() >= 3)
            {
                var temp = range.OrderBy(item => item).Select(i => i);
                var max = temp.Max();
                var min = temp.Min();
                var mid = temp.ElementAt(range.Count() / 2);
                var maxIndex = tempRange.IndexOf(max);
                var minIndex = tempRange.IndexOf(min);
                var midIndex = tempRange.IndexOf(mid);

                var m1 = m[maxIndex];
                var m2 = m[minIndex];
                var m3 = m[midIndex];

                var n1 = n[maxIndex];
                var n2 = n[minIndex];
                var n3 = n[midIndex];

                var a = (m1 - m2) * (m1 - m2) + (n1 - n2) * (n1 - n2);
                var b = (m2 - m3) * (m2 - m3) + (n2 - n3) * (n2 - n3);

                var temp1 = (m2 - m3) * (max * max - min * min);
                var temp2 = (m1 - m2) * (min * min - mid * mid);
                var temp3 = (n2 - n3) * (max * max - min * min);
                var temp4 = (n1 - n2) * (min * min - mid * mid);

                var x = ((temp3 - temp4) - (n2 - n3) * a + (n1 - n2) * b) / 2 / ((n1 - n2) * (m2 - m3) - (n2 - n3) * (m1 - m2));
                var y = ((temp1 - temp2) - (m2 - m3) * a + (m1 - m2) * b) / 2 / ((m1 - m2) * (n2 - n3) - (m2 - m3) * (n1 - n2));

                return new Tuple<double, double>(x, y);
            }
            else
            {
                return new Tuple<double, double>(100, 100);
            }

        }
    }
}
