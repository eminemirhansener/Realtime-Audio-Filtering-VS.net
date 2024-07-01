using System;
using System.Linq;
using System.Windows.Forms;
using System.Threading;
using NAudio.Wave;
using System.Numerics;
using Accord.Math;
using SoxSharp;
using SoxSharp.Effects.Types;
using SoxSharp.Effects;

namespace Audio_Signal_Processing_1
{
    public partial class Form1 : Form
    {
        private WaveIn wi;
        private BufferedWaveProvider bwp;
        private Thread soxLoopThread;
        private Thread soxRecordThread;
        private bool stopRequested = false;
        private int deviceNumber;  // Declared here
        private int SampleRate = 44100; // Sound Card Sampling Rate
        private int BufferSize = (int)Math.Pow(2, 11);
        private string file_name = @"Recordings\recorded.wav";

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                var inp_device = WaveIn.GetCapabilities(i);
                comboBox1.Items.Add(inp_device.ProductName);
            }

            comboBox1.SelectedIndex = 0; // Set default selected device
        }

        private void wi_DataAvailable(object sender, WaveInEventArgs e)
        {
            bwp.AddSamples(e.Buffer, 0, e.BytesRecorded);
        }

        private void updateAudioGraph()
        {
            int frameSize = BufferSize;
            var frames = new byte[frameSize];
            bwp.Read(frames, 0, frameSize);
            if (frames.Length == 0 || frames[frameSize - 2] == 0) return;

            timer1.Enabled = false;

            int SAMPLE_RESOLUTION = 16;
            int BYTES_PER_POINT = SAMPLE_RESOLUTION / 8;
            Int32[] vals = new Int32[frames.Length / BYTES_PER_POINT];
            double[] Ys = new double[frames.Length / BYTES_PER_POINT];
            double[] Xs = new double[frames.Length / BYTES_PER_POINT];
            double[] Ys2 = new double[frames.Length / BYTES_PER_POINT];
            double[] Xs2 = new double[frames.Length / BYTES_PER_POINT];
            for (int i = 0; i < vals.Length; i++)
            {
                byte hByte = frames[i * 2 + 1];
                byte lByte = frames[i * 2 + 0];
                vals[i] = (int)(short)((hByte << 8) | lByte);
                Xs[i] = i;
                Ys[i] = vals[i];
                Xs2[i] = (double)i / Ys.Length * SampleRate / 1000.0;
            }

            scottPlotUC1.Xs = Xs;
            scottPlotUC1.Ys = Ys;

            Ys2 = FFT(Ys);
            scottPlotUC2.Xs = Xs2.Take(Xs2.Length / 2).ToArray();
            scottPlotUC2.Ys = Ys2.Take(Ys2.Length / 2).ToArray();

            scottPlotUC1.UpdateGraph();
            scottPlotUC2.UpdateGraph();

            Application.DoEvents();
            scottPlotUC1.Update();
            scottPlotUC2.Update();

            calculatePeakFreq(Xs2, Ys2);

            timer1.Enabled = true;
        }

        private void sox_loop()
        {
            using (var sox = new Sox("C:\\Program Files (x86)\\sox-14-4-1\\sox.exe"))
            {
                try
                {
                    int highPassFreq = int.Parse(textBox1.Text);
                    int lowPassFreq = int.Parse(textBox2.Text);

                    sox.Effects.Add(new VolumeEffect(10, GainType.Db));
                    sox.Effects.Add(new LowPassFilterEffect(lowPassFreq));
                    sox.Effects.Add(new HighPassFilterEffect(highPassFreq));
                    sox.OnProgress += sox_OnProgress;
                    sox.Process("-d", "-d");
                }
                catch
                {
                    MessageBox.Show("Enter Valid Cutoff Frequency", "Invalid Cutoff Frequency");
                }
            }
        }

        private void sox_record()
        {
            string filename_current = file_name;
            int count = 0;

            using (var sox = new Sox("C:\\Program Files (x86)\\sox-14-4-1\\sox.exe"))
            {
                while (System.IO.File.Exists(filename_current))
                {
                    count++;
                    filename_current = System.IO.Path.GetDirectoryName(file_name)
                                     + System.IO.Path.DirectorySeparatorChar
                                     + System.IO.Path.GetFileNameWithoutExtension(file_name)
                                     + count.ToString()
                                     + System.IO.Path.GetExtension(file_name);
                }

                try
                {
                    int highPassFreq = int.Parse(textBox1.Text);
                    int lowPassFreq = int.Parse(textBox2.Text);

                    sox.Effects.Add(new VolumeEffect(10, GainType.Db));
                    sox.Effects.Add(new LowPassFilterEffect(lowPassFreq));
                    sox.Effects.Add(new HighPassFilterEffect(highPassFreq));
                    sox.OnProgress += sox_OnProgress;
                    sox.Process("-d", filename_current);
                }
                catch
                {
                    MessageBox.Show("Error during recording", "Recording Error");
                }
            }
        }

        public double[] FFT(double[] data)
        {
            double[] fft = new double[data.Length];
            Complex[] fftComplex = new Complex[data.Length];

            for (int i = 0; i < data.Length; i++)
            {
                fftComplex[i] = new Complex(data[i], 0.0);
            }

            FourierTransform.FFT(fftComplex, FourierTransform.Direction.Forward);

            for (int i = 0; i < data.Length; i++)
            {
                fft[i] = fftComplex[i].Magnitude;
            }

            return fft;
        }

        private void calculatePeakFreq(double[] XFreq, double[] YMag)
        {
            double max_valueMag = YMag.Max();
            int indexOfMaxMag = Array.IndexOf(YMag, max_valueMag);
            double max_valueFreqKHz = XFreq[indexOfMaxMag];
            double max_valueFreqHz = Math.Round(max_valueFreqKHz * 1000);

            if (max_valueMag > 0.5)
            {
                label6.Text = "Peak Frequency: " + max_valueFreqHz + " [Hz]";
            }
            else
            {
                label6.Text = "Peak Frequency: 0 [Hz]";
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            updateAudioGraph();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (comboBox1.SelectedIndex < 0)
            {
                MessageBox.Show("Select an Input Device", "Input Device Error");
                return;
            }

            deviceNumber = comboBox1.SelectedIndex;

            wi = new WaveIn();
            wi.DeviceNumber = deviceNumber;
            wi.WaveFormat = new WaveFormat(SampleRate, 1);
            wi.BufferMilliseconds = (int)((double)BufferSize / (double)SampleRate * 1000.0);

            wi.DataAvailable += wi_DataAvailable;
            bwp = new BufferedWaveProvider(wi.WaveFormat)
            {
                BufferLength = BufferSize * 2,
                DiscardOnBufferOverflow = true
            };

            wi.StartRecording();
            updateAudioGraph();
            timer1.Enabled = true;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            timer1.Enabled = false;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            stopRequested = false;

            soxLoopThread = new Thread(sox_loop);
            soxLoopThread.Start();

            soxRecordThread = new Thread(sox_record);
            soxRecordThread.Start();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            stopRequested = true;
        }

        void sox_OnProgress(object sender, ProgressEventArgs e)
        {
            if (stopRequested)
            {
                e.Abort = true;
                stopRequested = false; // Reset the flag
            }
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            stopRequested = true;
            soxLoopThread?.Join();
            soxRecordThread?.Join();
        }

        private void textBox1_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.Handled = !char.IsNumber(e.KeyChar);
        }

        private void textBox2_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.Handled = !char.IsNumber(e.KeyChar);
        }
    }
}
