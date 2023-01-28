using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;

using NAudio.Wave;
using NAudio.CoreAudioApi;
using System.Numerics;
using Accord.Math;
using NAudio.Dsp;

using complex = System.Numerics.Complex;
using System.IO;
using System.Security.Cryptography;
using SoxSharp.Effects.Types;
using SoxSharp.Effects;
using SoxSharp;
using System.Diagnostics;

namespace Audio_Signal_Processing_1
{
    public partial class Form1 : Form
    {

        public WaveIn wi;
        public BufferedWaveProvider bwp;
        public Int32 envelopeMax;
        public int deviceNumber;
        public int frequency = 4000;
        public int width = 3500;
        public bool btn_clckd;

        private int SampleRate = 44100; // Sound Card Sampling Rate
        private int BufferSize = (int)Math.Pow(2, 11);

        public Form1()
        {
            InitializeComponent();
        }
        // Datas are put into buffer.
        void wi_DataAvailable(object sender, WaveInEventArgs e)
        {
            bwp.AddSamples(e.Buffer, 0, e.BytesRecorded);
        }

        public void updateAudioGraph()
        {

            int frameSize = BufferSize;
            var frames = new byte[frameSize];
            bwp.Read(frames, 0, frameSize);
            if (frames.Length == 0) return;
            if (frames[frameSize - 2] == 0) return;

            timer1.Enabled = false;

            // convert it to int32 manually (and a double for scottplot)
            int SAMPLE_RESOLUTION = 16;
            int BYTES_PER_POINT = SAMPLE_RESOLUTION / 8;
            Int32[] vals = new Int32[frames.Length / BYTES_PER_POINT];
            double[] Ys = new double[frames.Length / BYTES_PER_POINT];
            double[] Xs = new double[frames.Length / BYTES_PER_POINT];
            double[] Ys2 = new double[frames.Length / BYTES_PER_POINT];
            double[] Xs2 = new double[frames.Length / BYTES_PER_POINT];
            for (int i = 0; i < vals.Length; i++)
            {
                // bit shift the byte buffer into the right variable format
                byte hByte = frames[i * 2 + 1];
                byte lByte = frames[i * 2 + 0];
                vals[i] = (int)(short)((hByte << 8) | lByte);
                Xs[i] = i;
                Ys[i] = vals[i];
                Xs2[i] = (double)i / Ys.Length * SampleRate / 1000.0; // units are in kHz
            }

            // update scottplot (PCM, time domain)
            scottPlotUC1.Xs = Xs;
            scottPlotUC1.Ys = Ys;

            //update scottplot (FFT, frequency domain)
            Ys2 = FFT(Ys);
            scottPlotUC2.Xs = Xs2.Take(Xs2.Length / 2).ToArray();
            scottPlotUC2.Ys = Ys2.Take(Ys2.Length / 2).ToArray();


            // update the displays
            scottPlotUC1.UpdateGraph();
            scottPlotUC2.UpdateGraph();

            Application.DoEvents();
            scottPlotUC1.Update();
            scottPlotUC2.Update();

            calculatePeakFreq(Xs2, Ys2);

            timer1.Enabled = true;

        }

        private void sox(int frequency, int width)
        {

            using (var sox = new Sox("C:\\Program Files (x86)\\sox-14-4-1\\sox.exe"))
            {
                sox.Effects.Add(new VolumeEffect(0, GainType.Db));
                sox.Effects.Add(new BandPassFilterEffect(frequency, width));
                sox.OnProgress += sox_OnProgress;
                sox.Process("-d", "-d");
            }

        }
        
        // FFT converting function.
        public double[] FFT(double[] data)
        {
            double[] fft = new double[data.Length];
            complex[] fftComplex = new complex[data.Length];

            for (int i = 0; i < data.Length; i++)
            {
                fftComplex[i] = new complex(data[i], 0.0);
            }

            Accord.Math.FourierTransform.FFT(fftComplex, Accord.Math.FourierTransform.Direction.Forward);

            for(int i = 0; i < data.Length; i++)
            {
                fft[i] = fftComplex[i].Magnitude;
                //fft[i] = 10 * Math.Log10(fft[i]); // to convert dB
            }

            return fft;
        }
        
        private void Form1_Load(object sender, EventArgs e)
        {
            //İnput Devices are shown in the combo box.
            for (int i = 0; i < NAudio.Wave.WaveIn.DeviceCount; i++)
            {
                var inp_device = NAudio.Wave.WaveIn.GetCapabilities(i);
                comboBox1.Items.Add(inp_device.ProductName);
            }

            deviceNumber  = comboBox1.SelectedIndex;
        }

        private void calculatePeakFreq(double[] XFreq, double[] YMag)
        {
            double max_valueMag = YMag.Max();
            int indexOfMaxMag = YMag.IndexOf(max_valueMag);
            double max_valueFreqKHz = XFreq[indexOfMaxMag];
            double max_valueFreqHz = Math.Round(max_valueFreqKHz * 1000);

            if(max_valueMag > 0.5)
            {
                label6.Text = "Peak Frequency: " + max_valueFreqHz + " [Hz]";
            }

            else
                label6.Text = "Peak Frequency: " + 0 + " [Hz]";
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            updateAudioGraph();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            WaveIn wi = new WaveIn();
            wi.DeviceNumber = deviceNumber;

            if (comboBox1.SelectedIndex < 0)
            {
                MessageBox.Show("Select an Input Device", "Input Device Error");
                return;
            }

            wi.WaveFormat = new NAudio.Wave.WaveFormat(SampleRate, 1);
            wi.BufferMilliseconds = (int)((double)BufferSize / (double)SampleRate * 1000.0);

            // create a wave buffer and start the recording
            wi.DataAvailable += new EventHandler<WaveInEventArgs>(wi_DataAvailable);
            bwp = new BufferedWaveProvider(wi.WaveFormat);
            bwp.BufferLength = BufferSize * 2;

            bwp.DiscardOnBufferOverflow = true;
            wi.StartRecording();

            updateAudioGraph();

            Thread threadSox = new Thread(() => sox(frequency, width));
            threadSox.Start();

            timer1.Enabled = true;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            timer1.Enabled = false;
            btn_clckd = true;
        }

        void sox_OnProgress(object sender, ProgressEventArgs e)
        {
            if (btn_clckd)
                e.Abort = true;

            btn_clckd = false;
        }
    }
}
