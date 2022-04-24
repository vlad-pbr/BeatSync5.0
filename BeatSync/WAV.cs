using System;
using System.Text;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Numerics;

namespace BeatSync
{
    /// <summary>
    /// Resources used by the codec to encode/decode bitstreams.
    /// Resources include various constants and helping functions.
    /// </summary>
    class Resources
    {
        public class FourierTransform
        {
            /// <summary>
            /// Computes a Discrete Fourier Transform over given signal samples.
            /// Algorithm solves DFT formula over each sample:
            /// 
            /// F(k) = 1 / Sqrt(N) * sigma(T(n) * e ^ (-2 * PI * n * k / N))
            /// 
            /// Where:
            /// F(k)    - frequency bin of a sample;
            /// T(n)    - current sample;
            /// n       - sample index;
            /// k       - frequency index;
            /// N       - amount of samples in a given signal.
            /// 
            /// Complexity: (n^2)
            /// 
            /// </summary>
            /// <param name="Signal">Array of amplitude samples of a signal.</param>
            /// <returns>Array of frequency bins of a signal.</returns>
            public static Complex[] DFT(Complex[] Signal)
            {
                // Define formula variables
                int N = Signal.Length, n, k;
                Complex[] Spectrum = new Complex[N];

                // Iterate over signal samples
                for (n = 0;
                    n < N;
                    n++)
                {
                    // Define frequency sigma variable
                    Complex cpxFrequencySigma = new Complex(0, 0);

                    // Calculate frequency sigma over each sample, per sample
                    for (k = 0;
                        k < N;
                        k++)
                    {
                        cpxFrequencySigma += Signal[k] * Complex.Exp(new Complex(0, -2 * Math.PI * n * k / N));
                    }

                    // Store calculated result
                    Spectrum[n] = (float)1 / Math.Sqrt(N) * cpxFrequencySigma;
                }

                // Return the spectrum
                return (Spectrum);
            }

            /// <summary>
            /// More efficient implementation of DFT
            /// </summary>
            public static void FFT(short dir, int m, double[] x, double[] y)
            {
                int n, i, i1, j, k, i2, l, l1, l2;
                double c1, c2, tx, ty, t1, t2, u1, u2, z;

                // Calculate the number of points

                n = 1;

                for (i = 0; i < m; i++)
                    n *= 2;

                // Do the bit reversal

                i2 = n >> 1;
                j = 0;
                for (i = 0; i < n - 1; i++)
                {
                    if (i < j)
                    {
                        tx = x[i];
                        ty = y[i];
                        x[i] = x[j];
                        y[i] = y[j];
                        x[j] = tx;
                        y[j] = ty;
                    }
                    k = i2;

                    while (k <= j)
                    {
                        j -= k;
                        k >>= 1;
                    }

                    j += k;
                }

                // Compute the FFT

                c1 = -1.0;
                c2 = 0.0;
                l2 = 1;

                for (l = 0; l < m; l++)
                {
                    l1 = l2;
                    l2 <<= 1;
                    u1 = 1.0;
                    u2 = 0.0;

                    for (j = 0; j < l1; j++)
                    {
                        for (i = j; i < n; i += l2)
                        {
                            i1 = i + l1;
                            t1 = u1 * x[i1] - u2 * y[i1];
                            t2 = u1 * y[i1] + u2 * x[i1];
                            x[i1] = x[i] - t1;
                            y[i1] = y[i] - t2;
                            x[i] += t1;
                            y[i] += t2;
                        }

                        z = u1 * c1 - u2 * c2;
                        u2 = u1 * c2 + u2 * c1;
                        u1 = z;
                    }

                    c2 = Math.Sqrt((1.0 - c1) / 2.0);

                    if (dir == 1)
                        c2 = -c2;

                    c1 = Math.Sqrt((1.0 + c1) / 2.0);
                }

                // Scaling for forward transform

                if (dir == 1)
                {
                    for (i = 0; i < n; i++)
                    {
                        x[i] /= n;
                        y[i] /= n;
                    }
                }
            }

            /// <summary>
            /// Computes an Inverse Discrete Fourier Transform over given signal samples.
            /// Algorithm solves Inverse DFT formula over each sample:
            /// 
            /// T(n) = 1 / Sqrt(N) * sigma(F(k) * e ^ (2 * PI * n * k / N))
            /// 
            /// Where:
            /// F(k)    - frequency bin of a sample;
            /// T(n)    - current sample;
            /// n       - sample index;
            /// k       - frequency index;
            /// N       - amount of samples in a given signal.
            /// 
            /// Complexity: (n^2)
            /// 
            /// </summary>
            /// <param name="Spectrum">Array of frequency bins of a signal.</param>
            /// <returns>Array of amplitude samples of a signal.</returns>
            public static Complex[] InverseDFT(Complex[] Spectrum)
            {
                // Define formula variables
                int N = Spectrum.Length, n, k;
                Complex[] Signal = new Complex[N];

                // Iterate over frequency bins
                for (n = 0;
                    n < N;
                    n++)
                {
                    // Define sample sigma variable
                    Complex cpxSample = new Complex(0, 0);

                    // Calculate sigma
                    for (k = 0;
                        k < N;
                        k++)
                    {
                        cpxSample += Spectrum[k] * Complex.Exp(new Complex(0, 2 * Math.PI * n * k / N));
                    }

                    // Store calculated result
                    Signal[n] = (float)1 / Math.Sqrt(N) * cpxSample;
                }

                // Return the signal
                return (Signal);
            }

        }

        // Bit-depths supported by this codec
        public static readonly int[] ALLOWED_BITDEPTHS =
        {
            sizeof(short) * 8,
            sizeof(float) * 8,
        };

        // Max amplitude for each supported bit-depth
        public static readonly Dictionary<Type, int> MAX_AMPLITUDE  = new Dictionary<Type, int>
        {
            { typeof(short[]),    short.MaxValue    },
            { typeof(float[]),    1                 }  
        };
        public const int GROUP_ID_LENGTH = 4;

        /// <summary>
        /// Checks if given file path is valid for reading and writing.
        /// </summary>
        /// <param name="sFilepath">Path to a file.</param>
        /// <returns>True if file path is valid, False otherwise.</returns>
        public static bool IsValidFilepath(string sFilepath)
        {
            // Try writing a dummy file to file path
            try
            {
                if (!File.Exists(sFilepath))
                {
                    File.WriteAllText(sFilepath, "");
                    File.Delete(sFilepath);
                }

                return (true);
            }

            // If file could not be written, file path is invalid
            catch
            {
                return (false);
            }
        }

        /// <summary>
        /// Returns index of the first occurence of target array in source array
        /// ranging from the start index to search limit index. If subarray was not found,
        /// returns -1.
        /// </summary>
        /// <param name="arrbSource">Byte array to look for subarray in.</param>
        /// <param name="arrbTarget">Byte array to look for in the source array.</param>
        /// <param name="nStartIndex">Index to begin searching from in the source array.</param>
        /// <param name="nSearchLimit">Amount of bytes to scan in the source array.</param>
        /// <returns>
        /// Index of the first occurence of target array in source array.
        /// If subarray was not found, returns -1.
        /// </returns>
        public static int IndexOf(byte[] arrbSource,
                                  byte[] arrbTarget,
                                  int nStartIndex,
                                  int nSearchLimit)
        {
            // If passed values are within limits
            if (arrbSource.Length >= arrbTarget.Length &&
                arrbTarget.Length > 0 &&
                arrbSource.Length - nStartIndex > nSearchLimit &&
                arrbSource.Length > nStartIndex)
            {
                // Iterate over source array from the given index
                for (int nIndexSource = nStartIndex;
                    nIndexSource < nStartIndex + nSearchLimit;
                    nIndexSource++)
                {
                    // If the first byte of target array was found in the source array
                    if (arrbSource[nIndexSource] == arrbTarget[0])
                    {
                        // Define a 'found' flag boolean as true
                        bool bFound = true;

                        // Iterate over target array
                        for (int nIndexTarget = 1;
                            nIndexTarget < arrbTarget.Length && bFound == true;
                            nIndexTarget++)
                        {
                            // If bytes do not match, mark 'found' flag as false
                            if (arrbSource[nIndexSource + nIndexTarget] != arrbTarget[nIndexTarget])
                            {
                                bFound = false;
                            }
                        }

                        // If found flag is true after the iteration, subarray was found
                        if (bFound == true)
                        {
                            return (nIndexSource);
                        }
                    }
                }
            }

            // If subarray was not found, return an illegal index value
            return (-1);
        }
    }

    /// <summary>
    /// Header information struct.
    /// </summary>
    public struct WAV_Header
    {
        public char[]   sGroupID;       // Chunk ID (should be 'RIFF')
        public uint     dwFileLength;   // File size - 8 (without Group ID and RIFF type)
        public char[]   sRiffType;      // Extension of a RIFF file (should be 'WAVE')
    }

    /// <summary>
    /// Format chunk information struct.
    /// </summary>
    public struct WAV_FormatChunk
    {
        public char[]   sGroupID;                   // Chunk ID (should be 'fmt ')
        public uint     dwChunkSize;                // Size of the rest of the chunk which follows this number
        public ushort   wFormatTag;                 // Sample format (should be 1 for 'PCM')
        public ushort   wChannels;                  // Amount of audio channels present
        public uint     dwSampleRate;               // Amount of samples per second of audio
        public uint     dwAverageBytesPerSecond;    // Average amount of bytes per second audio
        public ushort   wBlockAlign;                // Number of audio channels * Bits per Sample / 8
        public ushort   wBitDepth;                  // Amount of bits per audio sample
    }

    /// <summary>
    /// Data chunk information struct.
    /// </summary>
    public struct WAV_DataChunk
    {
        public char[]   sGroupID;       // Chunk ID (should be 'data')
        public uint     dwChunkSize;    // Number of bytes in the sample data portion
        public Array    sampleData;     // Array of audio samples
    }

    /// <summary>
    /// WAV file extension container.
    /// Class can decode valid WAV files, make changes to them and encode to disk.
    /// </summary>
    class WAV
    {
        // Audio container information variables
        private static WAV_Header       hHeader;
        private static WAV_FormatChunk  fcFormat;
        private static WAV_DataChunk    dcData;
        private static string           sFilePath = null;
        private static float            fFileDuration;
        private static long             lFileSize;
        private static int              nFileBPM;

        // Other related variables
        private static readonly int BPM_WINDOW_SIZE = 8;
        private static readonly int MIN_FILE_LENGTH = 36;

        /// <summary>
        /// Path to loaded audio file.
        /// </summary>
        public string Path
        {
            get
            {
                return sFilePath;
            }
        }

        /// <summary>
        /// Header chunk struct of audio.
        /// </summary>
        public WAV_Header Header
        {
            get
            {
                return hHeader;
            }
        }

        /// <summary>
        /// Format chunk struct of audio.
        /// </summary>
        public WAV_FormatChunk Format
        {
            get
            {
                return fcFormat;
            }
        }

        /// <summary>
        /// Data chunk struct of audio.
        /// </summary>
        public WAV_DataChunk Data
        {
            get
            {
                return dcData;
            }
        }

        /// <summary>
        /// Duration of audio in seconds.
        /// </summary>
        public double Duration
        {
            get
            {
                return (IsLoaded() ? Math.Round(fFileDuration, 3) : -1);
            }
        }

        /// <summary>
        /// Minimum audio length (in seconds) for BPM calculation.
        /// </summary>
        public static double BPM_CALCULATION_DURATION_MINIMUM
        {
            get
            {
                return BPM_WINDOW_SIZE * 3;
            }
        }

        /// <summary>
        /// Size of audio file in bytes.
        /// </summary>
        public long Size
        {
            get
            {
                return (IsLoaded() ? lFileSize : -1);
            }
        }

        /// <summary>
        /// Amount of samples per second of audio.
        /// </summary>
        public int SampleRate
        {
            get
            {
                return (IsLoaded() ? (int)fcFormat.dwSampleRate : -1);
            }
        }

        /// <summary>
        /// Amount of bits per audio sample.
        /// </summary>
        public int BitDepth
        {
            get
            {
                return (IsLoaded() ? fcFormat.wBitDepth : -1);
            }
        }

        /// <summary>
        /// Amount of Beats Per Minute in a given audio file.
        /// </summary>
        public int BPM
        {
            get
            {
                // Set window time to be 8 seconds
                int nWindow = BPM_WINDOW_SIZE;

                // If file is loaded, BPM was not yet calculated and song length is long enough
                if (IsLoaded() &&
                    nFileBPM == -1 &&
                    Duration >= nWindow * 3)
                {
                    // Calculate the amount of samples needed for 8 seconds of audio
                    int nSamples = SampleRate * nWindow;

                    // Create an array of window sample summations
                    double[] Peaks = new double[((int)Duration / 3) / nWindow];

                    // Define a separate sample array
                    Array AudioSignal = null;

                    // Copy samples to new sample array and normalize them
                    switch (Format.wBitDepth / 8)
                    {
                        case (sizeof(short)):
                            AudioSignal = Array.CreateInstance(typeof(short), Data.sampleData.Length);
                            Array.Copy(Data.sampleData, AudioSignal, AudioSignal.Length);
                            Samples.Normalize((short[])AudioSignal);
                            break;
                        case (sizeof(float)):
                            AudioSignal = Array.CreateInstance(typeof(float), Data.sampleData.Length);
                            Array.Copy(Data.sampleData, AudioSignal, AudioSignal.Length);
                            Samples.Normalize((float[])AudioSignal);
                            break;
                    }

                    // Go over the middle third of the audio data
                    for (int nWindowIndex = 0, nSampleIndex = AudioSignal.Length / 3;
                        nWindowIndex < Peaks.Length;
                        nWindowIndex++)
                    {
                        // Go over the current window
                        for (int nWindowEnd = nSampleIndex + nSamples;
                            nSampleIndex < nWindowEnd;
                            nSampleIndex++)
                        {
                            // Count the amount of 0db peaks in the current window
                            switch (Format.wBitDepth / 8)
                            {
                                case (sizeof(short)):
                                    Peaks[nWindowIndex] += Math.Abs((short)AudioSignal.GetValue(nSampleIndex) - 1) == Resources.MAX_AMPLITUDE[typeof(short[])] - 1 ? 1 : 0;
                                    break;
                                case (sizeof(float)):
                                    Peaks[nWindowIndex] += Math.Abs((float)AudioSignal.GetValue(nSampleIndex) - 1) == Resources.MAX_AMPLITUDE[typeof(float[])] - 1 ? 1 : 0;
                                    break;
                            }
                        }
                    }

                    // Get the index of the first sample of the "loudest" window
                    int nLeadSample = (Peaks.ToList().IndexOf(Peaks.Max()) * nWindow) + (Peaks.Length * nWindow) * SampleRate;

                    // Define variables for Fast Fourier Transform
                    int N = nSamples;
                    int nPower = 0;
                    int nIndex;

                    // Find a power of 2 that is the closest to the amount of samples in a window
                    for (nIndex = 1;
                        (nIndex * 2) < N;
                        nIndex *= 2, nPower++) ;

                    // Set the amount of samples to be 2 in power we just found
                    N = (int)Math.Pow(2, nPower);

                    // Define arrays for real and imaginary parts of the signal
                    double[] Real       = new double[N];
                    double[] Imaginary  = new double[N];

                    // Copy the amplitude values from audio data to the real part of the signal
                    switch (Format.wBitDepth / 8)
                    {
                        case (sizeof(short)):
                            Samples.LoadSamples(Real, (short[])AudioSignal, nLeadSample, N);
                            break;
                        case (sizeof(float)):
                            Samples.LoadSamples(Real, (float[])AudioSignal, nLeadSample, N);
                            break;
                    }

                    // Calculate a Fast Fourier Transform over given signal
                    Resources.FourierTransform.FFT(1, nPower, Real, Imaginary);

                    // Calculate a spectrum coefficient
                    float nSpectrumCoefficient = (SampleRate / (float)N) * 2;
                    int nSample;

                    // Reach the 250hz frequency bin
                    for (nSample = 0;
                        nSample * nSpectrumCoefficient < 250;
                        nSample++) ;

                    // Set every frequency bin beginning from 100hz to be 0
                    for (;
                        nSample < N;
                        nSample++)
                    {
                        Real[nSample] = Imaginary[nSample] = 0;
                    }

                    // Calculate an Inverse Fast Fourier Transform over given signal
                    Resources.FourierTransform.FFT(-1, nPower, Real, Imaginary);

                    // Define variables for lowpassed signal analysis
                    float[] Lowpass = new float[N];
                    double dAverage = 0;
                    double dSummation = 0;

                    // Iterate over each sample in the real array
                    for (nIndex = 0; nIndex < N; nIndex++)
                    {
                        // Copy lowpassed signal to the lowpass array, setting each negative value to 0
                        switch (Format.wBitDepth / 8)
                        {
                            case (sizeof(short)):
                                Lowpass[nIndex] = Math.Max((short)0, (short)Real[nIndex]);
                                break;
                            case (sizeof(float)):
                                Lowpass[nIndex] = Math.Max((float)0, (float)Real[nIndex]);
                                break;
                        }
                    }

                    // Normalize the received lowpass signal
                    switch (Format.wBitDepth / 8)
                    {
                        case (sizeof(short)):
                            // Convert the float array to short array
                            Samples.Normalize(Array.ConvertAll(Lowpass, new Converter<float, short>
                            (
                                // Cast each float sample to short sample
                                delegate (float fSample)
                                {
                                    return (short)fSample;
                                }
                            )));
                            break;
                        case (sizeof(float)):
                            Samples.Normalize(Lowpass);
                            break;
                    }

                    // Create a list of pairs for impulse matches
                    List<KeyValuePair<int, int>> Matches;

                    // Summate lowpassed signal amplitudes
                    for (nIndex = 0; nIndex < N; nIndex++)
                    {
                        dSummation += Lowpass[nIndex];
                    }

                    // Set the average multiplier to start from 7
                    int nMultiplier = 7;

                    // Start looking for bass drum impulses in the lowpassed signal.
                    // Amplitude threshold is calculated by taking the average amplitude value
                    // and multiplying it by the multiplier variable.
                    // If fewer than two matches were found above the threshold, lower the
                    // threshold value by decrementing the multiplier value and try again.
                    do
                    {
                        // Calculate average sample amplitude times the average multiplier
                        dAverage = dSummation / N * nMultiplier--;

                        // Initialize a new list of matches
                        Matches = new List<KeyValuePair<int, int>>();

                        // Calculate the jumping distance from an impulse
                        int nDistance = SampleRate / 4;

                        // Iterate over lowpass signal
                        for (nIndex = 0; nIndex < N; nIndex++)
                        {
                            // If an amplitude above the average value was found
                            if (Lowpass[nIndex] > dAverage)
                            {
                                // Find the next impulse
                                for (int nImpulse = nIndex + nDistance; nImpulse < N; nImpulse++)
                                {
                                    // If another impulse was found
                                    if (Lowpass[nImpulse] > dAverage)
                                    {
                                        // Make a match out of the first impulse index and
                                        // delta of two found impulses
                                        Matches.Add(new KeyValuePair<int, int>(nIndex, nImpulse - nIndex));
                                        nImpulse += nDistance;
                                    }
                                }

                                // Jump forward from the current impulse
                                nIndex += nDistance;
                            }
                        }
                    }
                    while (Matches.Count < 2 || nMultiplier < 0);

                    // If at least two matches exist
                    if (nMultiplier != 0)
                    {
                        // Define variables for the answer pair and jump counter
                        KeyValuePair<int, int> Answer = new KeyValuePair<int, int>(0, 0);
                        int nJumps = 0;

                        // Iterate over each impulse in the list of matches
                        foreach (KeyValuePair<int, int> Impulse in Matches)
                        {
                            // Start from the second impulse index
                            // Add delta from the pair to jump to the presumed index of the next impulse
                            for (nIndex = Impulse.Key + Impulse.Value; nIndex < N; nIndex += Impulse.Value)
                            {
                                // If jump landed on another impulse
                                if (Lowpass[nIndex] > dAverage)
                                {
                                    // Increment the jump counter
                                    nJumps++;
                                }

                                // Otherwise fulfill the loop condition
                                else
                                {
                                    nIndex = N;
                                }
                            }

                            // If successful jumps were made
                            if (nJumps > 0)
                            {
                                // Set the answer to be the current impulse delta if current answer's jump amount is lower
                                Answer = nJumps > Answer.Key ? new KeyValuePair<int, int>(nJumps, Impulse.Value) : Answer;
                            }

                            // Set the jump counter back to 0;
                            nJumps = 0;
                        }

                        // To calculate BPM using the sample delta, use formula:
                        // BPM = 60 / (Delta / Sample Rate)
                        nFileBPM = (int)(60 / ((float)Answer.Value / SampleRate));

                        // If BPM is lower than 100 or bigger than 200, scale it up or down appropriately
                        while (nFileBPM < 100 || nFileBPM > 200)
                        {
                            nFileBPM = nFileBPM < 100 ? nFileBPM * 2 : nFileBPM > 200 ? nFileBPM / 2 : nFileBPM;
                        }
                    }
                }

                return (nFileBPM);
            }
        }

        /// <summary>
        /// A set of functions that work with uncompressed audio samples.
        /// </summary>
        class Samples
        {
            /// <summary>
            /// Loads samples from byte array to short array.
            /// </summary>
            public static void LoadSamples(short[] Destination, byte[] Source, int nSampleDataIndex)
            {
                // Calculate sample size in bytes
                int nSampleSize = sizeof(short);

                // Load destination array with converted samples
                for (int nSampleIndex = 0;
                    nSampleIndex < Destination.Length;
                    nSampleIndex++)
                {
                    Destination[nSampleIndex] = BitConverter.ToInt16(Source, nSampleDataIndex + (nSampleIndex * nSampleSize));
                }
            }

            /// <summary>
            /// Loads samples from byte array to float array.
            /// </summary>
            public static void LoadSamples(float[] Destination, byte[] Source, int nSampleDataIndex)
            {
                // Calculate sample size in bytes
                int nSampleSize = sizeof(float);

                // Load destination array with converted samples
                for (int nSampleIndex = 0;
                    nSampleIndex < Destination.Length;
                    nSampleIndex++)
                {
                    Destination[nSampleIndex] = BitConverter.ToSingle(Source, nSampleDataIndex + (nSampleIndex * nSampleSize));
                }
            }

            /// <summary>
            /// Loads samples from short array to double array.
            /// </summary>
            public static void LoadSamples(double[] Destination, short[] Source, int nSampleDataIndex, int nSampleAmount)
            {
                // Load destination array with source samples
                for (int nSampleIndex = 0;
                    nSampleIndex < nSampleAmount;
                    nSampleIndex++)
                {
                    Destination[nSampleIndex] = Source[nSampleDataIndex + nSampleIndex];
                }
            }

            /// <summary>
            /// Loads samples from float array to double array.
            /// </summary>
            public static void LoadSamples(double[] Destination, float[] Source, int nSampleDataIndex, int nSampleAmount)
            {
                // Load destination array with source samples
                for (int nSampleIndex = 0;
                    nSampleIndex < nSampleAmount;
                    nSampleIndex++)
                {
                    Destination[nSampleIndex] = Source[nSampleDataIndex + nSampleIndex];
                }
            }

            /// <summary>
            /// Normalized a signal of short samples.
            /// </summary>
            /// <param name="Samples">Signal of short samples.</param>
            public static void Normalize(short[] Samples)
            {
                // Define a max amplitude variable
                int nMaxAmplitude = 1;

                // Iterate over sample array and find highest amplitude value
                for (int nSampleIndex = 0;
                    nSampleIndex < Samples.Length;
                    nSampleIndex++)
                {
                    nMaxAmplitude = Math.Abs((int)Samples[nSampleIndex]) > nMaxAmplitude ?
                                    Math.Abs((int)Samples[nSampleIndex]) : nMaxAmplitude;
                }

                // Calculate sample multiplication coefficient
                float fCoefficient = Resources.MAX_AMPLITUDE[typeof(short[])] / (float)nMaxAmplitude;
                
                // If samples are not already normalized
                if (fCoefficient > 1)
                {
                    // Iterate over sample array and multiply each sample amplitude by coefficient
                    for (int nSampleIndex = 0;
                        nSampleIndex < Samples.Length;
                        nSampleIndex++)
                    {
                        Samples[nSampleIndex] = (short)(Samples[nSampleIndex] * fCoefficient);
                    }
                }
            }

            /// <summary>
            /// Normalizes a signal of float samples.
            /// </summary>
            /// <param name="Samples">Signal of float samples.</param>
            public static void Normalize(float[] Samples)
            {
                // Define a max amplitude variable
                float fMaxAmplitude = 1;

                // Iterate over sample array and find highest amplitude value
                for (int nSampleIndex = 0;
                    nSampleIndex < Samples.Length;
                    nSampleIndex++)
                {
                    fMaxAmplitude = Math.Abs(Samples[nSampleIndex]) > fMaxAmplitude ?
                                    Math.Abs(Samples[nSampleIndex]) : fMaxAmplitude;
                }

                // Calculate sample multiplication coefficient
                float fCoefficient = Resources.MAX_AMPLITUDE[typeof(float[])] / fMaxAmplitude;

                // If samples are not already normalized
                if (fCoefficient != 1)
                {
                    // Iterate over sample array and multiply each sample amplitude by coefficient
                    for (int nSampleIndex = 0;
                        nSampleIndex < Samples.Length;
                        nSampleIndex++)
                    {
                        Samples[nSampleIndex] *= fCoefficient;
                    }
                }
            }

            /// <summary>
            /// Reverses polarity of short signal.
            /// </summary>
            /// <param name="Samples">Short signal.</param>
            public static void ReversePolarity(short[] Samples)
            {
                // Iterate over sample array and multiply each amplitude by -1
                for (int nSampleIndex = 0;
                    nSampleIndex < Samples.Length;
                    nSampleIndex++)
                {
                    Samples[nSampleIndex] = (short)-Samples[nSampleIndex];
                }
            }

            /// <summary>
            /// Reverses polarity of float signal.
            /// </summary>
            /// <param name="Samples">Float signal.</param>
            public static void ReversePolarity(float[] Samples)
            {
                // Iterate over sample array and multiply each amplitude by -1
                for (int nSampleIndex = 0;
                    nSampleIndex < Samples.Length;
                    nSampleIndex++)
                {
                    Samples[nSampleIndex] = -Samples[nSampleIndex];
                }
            }

            /// <summary>
            /// Reverses a signal of short samples.
            /// </summary>
            /// <param name="Samples">Signal of short samples.</param>
            public static void Reverse(short[] Samples)
            {
                // Define a temporary sample value
                short nSample;

                // Iterate over half of the sample array
                for (int nSampleIndex = 0;
                    nSampleIndex < Samples.Length / 2;
                    nSampleIndex++)
                {
                    // Swap the given sample with a mirrored sample from the end
                    nSample = Samples[Samples.Length - nSampleIndex - 1];
                    Samples[Samples.Length - nSampleIndex - 1] = Samples[nSampleIndex];
                    Samples[nSampleIndex] = nSample;
                }
            }

            /// <summary>
            /// Reverses a signal of float samples.
            /// </summary>
            /// <param name="Samples">Signal of float samples.</param>
            public static void Reverse(float[] Samples)
            {
                // Define a temporary sample value
                float nSample;

                // Iterate over half of the sample array
                for (int nSampleIndex = 0;
                    nSampleIndex < Samples.Length / 2;
                    nSampleIndex++)
                {
                    // Swap the given sample with a mirrored sample from the end
                    nSample = Samples[Samples.Length - nSampleIndex - 1];
                    Samples[Samples.Length - nSampleIndex - 1] = Samples[nSampleIndex];
                    Samples[nSampleIndex] = nSample;
                }
            }

            /// <summary>
            /// Writes samples from short array to byte buffer.
            /// </summary>
            public static void WriteSamples(byte[] Buffer, short[] Samples, int nBufferIndex)
            {
                // Calculate sample size
                int nSampleSize = fcFormat.wBitDepth / 8;

                // Iterate over each sample, convert it to bytes and write to buffer
                for (int nSampleIndex = 0;
                    nSampleIndex < Samples.Length;
                    nSampleIndex++)
                {
                    BitConverter.GetBytes(Samples[nSampleIndex]).CopyTo(Buffer, nBufferIndex + (nSampleIndex * nSampleSize));
                }
            }

            /// <summary>
            /// Writes samples from float array to byte buffer.
            /// </summary>
            public static void WriteSamples(byte[] Buffer, float[] Samples, int nBufferIndex)
            {
                // Calculate sample size
                int nSampleSize = fcFormat.wBitDepth / 8;

                // Iterate over each sample, convert it to bytes and write to buffer
                for (int nSampleIndex = 0;
                    nSampleIndex < Samples.Length;
                    nSampleIndex++)
                {
                    BitConverter.GetBytes(Samples[nSampleIndex]).CopyTo(Buffer, nBufferIndex + (nSampleIndex * nSampleSize));
                }
            }
        }

        /// <summary>
        /// Builds a WAV container for a given WAVE file.
        /// </summary>
        /// <param name="sPathToFile">Path to an audio file.</param>
        public WAV(string sPathToFile)
        {
            Load(sPathToFile);
        }

        /// <summary>
        /// Builds a WAV container out of a given WAV container.
        /// </summary>
        /// <param name="waveFile">Given WAV container.</param>
        public WAV(WAV waveFile)
        {
            if (waveFile.IsLoaded())
            {
                // Unload previous file information
                this.Unload();

                // Fill in WAV header information
                hHeader = new WAV_Header
                {
                    sGroupID        = waveFile.Header.sGroupID,
                    dwFileLength    = waveFile.Header.dwFileLength,
                    sRiffType       = waveFile.Header.sRiffType
                };

                // Fill in WAV format chunk information
                fcFormat = new WAV_FormatChunk
                {
                    sGroupID                = waveFile.Format.sGroupID,
                    dwChunkSize             = waveFile.Format.dwChunkSize,
                    wFormatTag              = waveFile.Format.wFormatTag,
                    wChannels               = waveFile.Format.wChannels,
                    dwSampleRate            = waveFile.Format.dwSampleRate,
                    dwAverageBytesPerSecond = waveFile.Format.dwAverageBytesPerSecond,
                    wBlockAlign             = waveFile.Format.wBlockAlign,
                    wBitDepth               = waveFile.Format.wBitDepth
                };

                // Fill in WAV data chunk information
                dcData = new WAV_DataChunk
                {
                    sGroupID    = waveFile.Data.sGroupID,
                    dwChunkSize = waveFile.Data.dwChunkSize
                };

                // Set file path to be a non-null value
                sFilePath = "N/A";

                // Decide upon the sample array type and load it with samples
                switch (fcFormat.wBitDepth / 8)
                {
                    case (sizeof(short)):
                        dcData.sampleData = Array.CreateInstance(typeof(short), (dcData.dwChunkSize * 8) / fcFormat.wBitDepth);
                        break;
                    case (sizeof(float)):
                        dcData.sampleData = Array.CreateInstance(typeof(float), (dcData.dwChunkSize * 8) / fcFormat.wBitDepth);
                        break;
                    default:
                        Unload();
                        break;
                }

                // If sample data was loaded successfully
                if (IsLoaded())
                {
                    // Copy audio samples to a new WAV
                    waveFile.Data.sampleData.CopyTo(dcData.sampleData, 0);

                    // Calculate new file size
                    lFileSize = System.Runtime.InteropServices.Marshal.SizeOf(hHeader) +            // Header size
                                System.Runtime.InteropServices.Marshal.SizeOf(fcFormat) +           // Format chunk size
                                (sizeof(byte) * Resources.GROUP_ID_LENGTH) +                        // 'Group ID' variable type size
                                System.Runtime.InteropServices.Marshal.SizeOf(dcData.dwChunkSize) + // 'Chunk size' variable type size
                                dcData.dwChunkSize;

                    // Set file path to be a non-null value
                    sFilePath = "N/A";

                    // WAV File Duration = Data Chunk Size / Average Bytes per Second
                    fFileDuration = (float)dcData.dwChunkSize / fcFormat.dwAverageBytesPerSecond;
                }
            }
        }

        /// <summary>
        /// Builds a WAV container out of sample array, sample rate, channel amount and bit-depth.
        /// </summary>
        /// <param name="Data">Audio samples.</param>
        /// <param name="nSampleRate">Sample rate.</param>
        /// <param name="nChannels">Amount of channels.</param>
        /// <param name="nBitDepth">Bit-depth.</param>
        public WAV(byte[] Data,
                   uint nSampleRate,
                   ushort nChannels,
                   ushort nBitDepth)
        {
            // Unload previous file information
            Unload();

            // Fill in WAV header information
            hHeader = new WAV_Header
            {
                sGroupID        = new char[] { 'R', 'I', 'F', 'F' },
                dwFileLength    = 0,
                sRiffType       = new char[] { 'W', 'A', 'V', 'E' }
            };

            // Fill in WAV format chunk information
            fcFormat = new WAV_FormatChunk
            {
                sGroupID                = new char[] { 'f', 'm', 't', ' ' },
                dwChunkSize             = 16,
                wFormatTag              = 1,
                wChannels               = nChannels,
                dwSampleRate            = nSampleRate,
                dwAverageBytesPerSecond = nSampleRate * nChannels * nBitDepth / 8,
                wBlockAlign             = (ushort)(nChannels * nBitDepth / 8),
                wBitDepth               = nBitDepth
            };

            // Fill in WAV data chunk information
            dcData = new WAV_DataChunk
            {
                sGroupID    = new char[] { 'd', 'a', 't', 'a' },
                dwChunkSize = (uint)Data.Length
            };

            // Set file path to be a non-null value
            sFilePath = "N/A";

            // Decide upon the sample array type and load it with samples
            switch (fcFormat.wBitDepth / 8)
            {
                case (sizeof(short)):
                    dcData.sampleData = Array.CreateInstance(typeof(short), (dcData.dwChunkSize * 8) / fcFormat.wBitDepth);
                    Samples.LoadSamples((short[])dcData.sampleData, Data, 0);
                    break;
                case (sizeof(float)):
                    dcData.sampleData = Array.CreateInstance(typeof(float), (dcData.dwChunkSize * 8) / fcFormat.wBitDepth);
                    Samples.LoadSamples((float[])dcData.sampleData, Data, 0);
                    break;
                default:
                    Unload();
                    break;
            }

            // If samples were loaded successfully
            if (IsLoaded())
            {
                // Calculate new file size
                lFileSize = System.Runtime.InteropServices.Marshal.SizeOf(hHeader) +            // Header size
                            System.Runtime.InteropServices.Marshal.SizeOf(fcFormat) +           // Format chunk size
                            (sizeof(byte) * Resources.GROUP_ID_LENGTH) +                        // 'Group ID' variable type size
                            System.Runtime.InteropServices.Marshal.SizeOf(dcData.dwChunkSize) + // 'Chunk size' variable type size
                            dcData.dwChunkSize;

                // WAV File Duration = Data Chunk Size / Average Bytes per Second
                fFileDuration = (float)dcData.dwChunkSize / fcFormat.dwAverageBytesPerSecond;
            }
        }

        /// <summary>
        /// Checks if given file is a WAV file.
        /// </summary>
        /// <param name="sPathToFile">Path to a file.</param>
        /// <returns>True if given file is a valid WAV file, False otherwise.</returns>
        public static bool IsWAV(string sPathToFile)
        {
            // If file exists and has a minimum amount of bytes
            if (File.Exists(sPathToFile) &&
                new FileInfo(sPathToFile).Length > MIN_FILE_LENGTH)
            {
                // Open a stream for a given file and read the needed amount of bytes
                FileStream fsStream = File.OpenRead(sPathToFile);
                byte[] arrbBuffer = new byte[MIN_FILE_LENGTH];
                fsStream.Read(arrbBuffer, 0, arrbBuffer.Length);

                // Check if:
                //  - File is a RIFF file
                //  - RIFF type is WAVE
                //  - This codec can work with this file's bit depth
                return (Encoding.ASCII.GetString(arrbBuffer, 0, 4).Equals("RIFF") &&
                        Encoding.ASCII.GetString(arrbBuffer, 8, 4).Equals("WAVE") &&
                        Resources.ALLOWED_BITDEPTHS.Contains(System.BitConverter.ToUInt16(arrbBuffer, 34)));
            }

            return (false);
        }

        /// <summary>
        /// Loads audio information into the given instance.
        /// </summary>
        /// <param name="sPathToFile">Path to an audio file (include extension).</param>
        /// <returns>True if file was loaded successfully, False otherwise.</returns>
        public bool Load(string sPathToFile)
        {
            // If file exists AND is a valid WAV file
            if (File.Exists(sPathToFile) &&
                IsWAV(sPathToFile))
            {
                // Read all bytes from a given WAV file
                FileStream fs = File.OpenRead(sPathToFile);
                byte[] arrbFileData = File.ReadAllBytes(sPathToFile);
                int nFileDataIndex;

                // Unload previous file information
                Unload();

                // Fill in WAV header information
                hHeader = new WAV_Header
                {
                    sGroupID        = Encoding.ASCII.GetChars(arrbFileData,        (nFileDataIndex = 0), sizeof(byte) * Resources.GROUP_ID_LENGTH),
                    dwFileLength    = System.BitConverter.ToUInt32(arrbFileData,   (nFileDataIndex += sizeof(byte) * Resources.GROUP_ID_LENGTH)),
                    sRiffType       = Encoding.ASCII.GetChars(arrbFileData,        (nFileDataIndex += sizeof(System.Int32)), Resources.GROUP_ID_LENGTH)
                };

                // Find format chunk index
                if ((nFileDataIndex = Resources.IndexOf(arrbFileData,
                                                        Encoding.ASCII.GetBytes("fmt "),
                                                        (nFileDataIndex += sizeof(byte) * Resources.GROUP_ID_LENGTH),
                                                        System.Runtime.InteropServices.Marshal.SizeOf(fcFormat))) == -1)
                {
                    return (false);
                }

                // Fill in WAV format chunk information
                fcFormat = new WAV_FormatChunk
                {
                    sGroupID                = Encoding.ASCII.GetChars(arrbFileData,          nFileDataIndex, sizeof(byte) * Resources.GROUP_ID_LENGTH),
                    dwChunkSize             = System.BitConverter.ToUInt32(arrbFileData,    (nFileDataIndex += sizeof(byte) * Resources.GROUP_ID_LENGTH)),
                    wFormatTag              = System.BitConverter.ToUInt16(arrbFileData,    (nFileDataIndex += sizeof(System.Int32))),
                    wChannels               = System.BitConverter.ToUInt16(arrbFileData,    (nFileDataIndex += sizeof(System.Int16))),
                    dwSampleRate            = System.BitConverter.ToUInt32(arrbFileData,    (nFileDataIndex += sizeof(System.Int16))),
                    dwAverageBytesPerSecond = System.BitConverter.ToUInt32(arrbFileData,    (nFileDataIndex += sizeof(System.Int32))),
                    wBlockAlign             = System.BitConverter.ToUInt16(arrbFileData,    (nFileDataIndex += sizeof(System.Int32))),
                    wBitDepth               = System.BitConverter.ToUInt16(arrbFileData,    (nFileDataIndex += sizeof(System.Int16)))
                };

                // Find data chunk index
                if ((nFileDataIndex = Resources.IndexOf(arrbFileData,
                                                        Encoding.ASCII.GetBytes("data"),
                                                        (nFileDataIndex += sizeof(System.Int16)),
                                                        nFileDataIndex)) == -1)
                {
                    return (false);
                }

                // Fill in WAV data chunk information
                dcData = new WAV_DataChunk
                {
                    sGroupID    = Encoding.ASCII.GetChars(arrbFileData,         nFileDataIndex, sizeof(byte) * Resources.GROUP_ID_LENGTH),
                    dwChunkSize = System.BitConverter.ToUInt32(arrbFileData,   (nFileDataIndex += sizeof(byte) * Resources.GROUP_ID_LENGTH)),
                };

                // Shift data index to sample data start
                nFileDataIndex += sizeof(System.Int32);

                // Decide upon the sample array type and load it with samples
                switch (fcFormat.wBitDepth / 8)
                {
                    case (sizeof(short)):
                        dcData.sampleData = Array.CreateInstance(typeof(short), (dcData.dwChunkSize * 8) / fcFormat.wBitDepth);
                        Samples.LoadSamples((short[])dcData.sampleData, arrbFileData, nFileDataIndex);
                        break;
                    case (sizeof(float)):
                        dcData.sampleData = Array.CreateInstance(typeof(float), (dcData.dwChunkSize * 8) / fcFormat.wBitDepth);
                        Samples.LoadSamples((float[])dcData.sampleData, arrbFileData, nFileDataIndex);
                        break;
                    default:
                        Unload();
                        return (false);
                }

                // Fill in basic file information
                sFilePath = sPathToFile;

                // Calculate new file size
                lFileSize = System.Runtime.InteropServices.Marshal.SizeOf(hHeader) +            // Header size
                            System.Runtime.InteropServices.Marshal.SizeOf(fcFormat) +           // Format chunk size
                            (sizeof(byte) * Resources.GROUP_ID_LENGTH) +                        // 'Group ID' variable type size
                            System.Runtime.InteropServices.Marshal.SizeOf(dcData.dwChunkSize) + // 'Chunk size' variable type size
                            dcData.dwChunkSize;                                                 // Actual sample data size in bytes

                // WAV File Duration = Data Chunk Size / Average Bytes per Second
                fFileDuration = (float)dcData.dwChunkSize / fcFormat.dwAverageBytesPerSecond;

                return (true);
            }

            return (false);
        }

        /// <summary>
        /// Unloads WAV instance.
        /// </summary>
        public void Unload()
        {
            // Reset class values
            hHeader     = new WAV_Header();
            fcFormat    = new WAV_FormatChunk();
            dcData      = new WAV_DataChunk();
            nFileBPM    = -1;
            sFilePath   = null;
        }

        /// <summary>
        /// Checks if a file is currently loaded within this instance.
        /// </summary>
        /// <returns>True if a file is loaded, False otherwise.</returns>
        public bool IsLoaded()
        {
            return (sFilePath != null);
        }

        /// <summary>
        /// Normalizes loaded audio.
        /// </summary>
        public void Normalize()
        {
            if (IsLoaded())
            {
                // Normalize audio based on bit depth
                switch (fcFormat.wBitDepth / 8)
                {
                    case (sizeof(short)):
                        Samples.Normalize((short[])dcData.sampleData);
                        break;
                    case (sizeof(float)):
                        Samples.Normalize((float[])dcData.sampleData);
                        break;
                }
            }
        }

        /// <summary>
        /// Reverses polarity of loaded audio.
        /// </summary>
        public void ReversePolarity()
        {
            if (IsLoaded())
            {
                // Reverse polarity of audio based on bit depth
                switch (fcFormat.wBitDepth / 8)
                {
                    case (sizeof(short)):
                        Samples.ReversePolarity((short[])dcData.sampleData);
                        break;
                    case (sizeof(float)):
                        Samples.ReversePolarity((float[])dcData.sampleData);
                        break;
                }
            }
        }

        /// <summary>
        /// Reverses loaded audio.
        /// </summary>
        public void Reverse()
        {
            if (IsLoaded())
            {
                // Reverse audio based on bit depth
                switch (fcFormat.wBitDepth / 8)
                {
                    case (sizeof(short)):
                        Samples.Reverse((short[])dcData.sampleData);
                        break;
                    case (sizeof(float)):
                        Samples.Reverse((float[])dcData.sampleData);
                        break;
                }
            }
        }

        /// <summary>
        /// Writes loaded audio in its current state to given destination.
        /// </summary>
        /// <param name="sFileDestination">Path to write audio to (include extension).</param>
        /// <returns>True if file was written successfully, False otherwise.</returns>
        public bool Write(string sFileDestination)
        {
            // If file is currently loaded AND given file path is valid
            if (IsLoaded() &&
                Resources.IsValidFilepath(sFileDestination))
            {
                // Create a new output stream buffer
                byte[] outStream = new byte[lFileSize];
                int nOutStreamIndex;

                // Write WAV header information
                Encoding.ASCII.GetBytes(hHeader.sGroupID).CopyTo(outStream,     (nOutStreamIndex = 0));
                BitConverter.GetBytes(lFileSize - 8).CopyTo(outStream,          (nOutStreamIndex += sizeof(byte) * Resources.GROUP_ID_LENGTH));
                Encoding.ASCII.GetBytes(hHeader.sRiffType).CopyTo(outStream,    (nOutStreamIndex += sizeof(System.Int32)));

                // Write WAV format chunk information
                Encoding.ASCII.GetBytes(fcFormat.sGroupID).CopyTo(outStream,                (nOutStreamIndex += sizeof(byte) * Resources.GROUP_ID_LENGTH));
                BitConverter.GetBytes(fcFormat.dwChunkSize).CopyTo(outStream,               (nOutStreamIndex += sizeof(byte) * Resources.GROUP_ID_LENGTH));
                BitConverter.GetBytes(fcFormat.wFormatTag).CopyTo(outStream,                (nOutStreamIndex += sizeof(System.Int32)));
                BitConverter.GetBytes(fcFormat.wChannels).CopyTo(outStream,                 (nOutStreamIndex += sizeof(System.Int16)));
                BitConverter.GetBytes(fcFormat.dwSampleRate).CopyTo(outStream,              (nOutStreamIndex += sizeof(System.Int16)));
                BitConverter.GetBytes(fcFormat.dwAverageBytesPerSecond).CopyTo(outStream,   (nOutStreamIndex += sizeof(System.Int32)));
                BitConverter.GetBytes(fcFormat.wBlockAlign).CopyTo(outStream,               (nOutStreamIndex += sizeof(System.Int32)));
                BitConverter.GetBytes(fcFormat.wBitDepth).CopyTo(outStream,                 (nOutStreamIndex += sizeof(System.Int16)));

                // Write WAV data chunk information
                Encoding.ASCII.GetBytes(dcData.sGroupID).CopyTo(outStream,  (nOutStreamIndex += sizeof(System.Int16)));
                BitConverter.GetBytes(dcData.dwChunkSize).CopyTo(outStream, (nOutStreamIndex += sizeof(byte) * Resources.GROUP_ID_LENGTH));

                // Shift stream index to sample data start
                nOutStreamIndex += sizeof(System.Int32);

                // Write PCM samples to buffer
                switch (fcFormat.wBitDepth / 8)
                {
                    case (sizeof(short)):
                        Samples.WriteSamples(outStream, (short[])dcData.sampleData, nOutStreamIndex);
                        break;
                    case (sizeof(float)):
                        Samples.WriteSamples(outStream, (float[])dcData.sampleData, nOutStreamIndex);
                        break;
                }

                // Write byte stream to disk
                File.WriteAllBytes(sFileDestination, outStream);

                // Return true if file was written successfully
                return (File.Exists(sFileDestination));
            }

            return (false);
        }

        /// <summary>
        /// Gives information about the currently loaded audio.
        /// </summary>
        /// <returns>Returns information about the currently loaded audio.</returns>
        public override string ToString()
        {
            if (IsLoaded())
            {
                return ("Path: " + sFilePath + "\n" +
                        "Duration: " + fFileDuration + " (s)\n" +
                        "Size: " + lFileSize / (float)1000 + " (kb)\n" +
                        "BPM: " + BPM + "\n" +
                        "Sample Rate: " + fcFormat.dwSampleRate + " (Hz)\n" +
                        "Bit Depth: " + fcFormat.wBitDepth + " (bits per sample)\n" +
                        "Channels: " + fcFormat.wChannels);
            }

            return ("No file loaded.");
        }
    }
}