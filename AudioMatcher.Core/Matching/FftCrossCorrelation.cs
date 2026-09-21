using System.Numerics;
using MathNet.Numerics.IntegralTransforms;

namespace AudioMatcher.Core.Matching;

internal static class FftCrossCorrelation
{
    public static float[] ZeroMeanNormalized(float[] sample, float[] target)
    {
        var sampleLength = sample.Length;
        var targetLength = target.Length;
        var valid = targetLength - sampleLength + 1;
        if (sampleLength == 0 || valid <= 0)
        {
            return [];
        }

        var sampleZeroMean = Center(sample, out var sampleNorm);
        if (sampleNorm < 1e-12)
        {
            return new float[valid];
        }

        var prefix = new double[targetLength + 1];
        var prefixSq = new double[targetLength + 1];
        for (var i = 0; i < targetLength; i++)
        {
            var value = target[i];
            prefix[i + 1] = prefix[i] + value;
            prefixSq[i + 1] = prefixSq[i] + (value * (double)value);
        }

        var correlation = new float[valid];
        var chunk = ChooseChunkSize(sampleLength, targetLength);
        for (var offset = 0; offset < valid; offset += chunk)
        {
            var count = Math.Min(chunk, valid - offset);
            var signalLength = count + sampleLength - 1;
            var raw = Correlate(sampleZeroMean, target, offset, signalLength);
            Array.Copy(raw, 0, correlation, offset, count);
        }

        for (var i = 0; i < valid; i++)
        {
            var windowSum = prefix[i + sampleLength] - prefix[i];
            var windowSumSq = prefixSq[i + sampleLength] - prefixSq[i];
            var windowVar = windowSumSq - (windowSum * windowSum / sampleLength);
            if (windowVar < 1e-12)
            {
                correlation[i] = 0;
                continue;
            }

            var ncc = correlation[i] / (sampleNorm * Math.Sqrt(windowVar));
            if (ncc > 1.0001)
            {
                ncc = 1;
            }
            else if (ncc < -1.0001)
            {
                ncc = -1;
            }

            correlation[i] = (float)ncc;
        }

        return correlation;
    }

    private static float[] Center(float[] sample, out double norm)
    {
        double sum = 0;
        foreach (var value in sample)
        {
            sum += value;
        }

        var mean = sum / sample.Length;
        var centered = new float[sample.Length];
        double energy = 0;
        for (var i = 0; i < sample.Length; i++)
        {
            var value = (float)(sample[i] - mean);
            centered[i] = value;
            energy += value * (double)value;
        }

        norm = Math.Sqrt(energy);
        return centered;
    }

    private static float[] Correlate(float[] kernel, float[] signal, int signalOffset, int signalLength)
    {
        var kernelLength = kernel.Length;
        var outputLength = signalLength - kernelLength + 1;
        var fftSize = 1;
        while (fftSize < signalLength + kernelLength - 1)
        {
            fftSize <<= 1;
        }

        var kernelFft = new Complex[fftSize];
        var signalFft = new Complex[fftSize];
        for (var i = 0; i < kernelLength; i++)
        {
            kernelFft[i] = kernel[kernelLength - 1 - i];
        }

        for (var i = 0; i < signalLength; i++)
        {
            signalFft[i] = signal[signalOffset + i];
        }

        Fourier.Forward(kernelFft, FourierOptions.AsymmetricScaling);
        Fourier.Forward(signalFft, FourierOptions.AsymmetricScaling);
        for (var i = 0; i < fftSize; i++)
        {
            signalFft[i] *= kernelFft[i];
        }

        Fourier.Inverse(signalFft, FourierOptions.AsymmetricScaling);

        var result = new float[outputLength];
        for (var i = 0; i < outputLength; i++)
        {
            result[i] = (float)signalFft[i + kernelLength - 1].Real;
        }

        return result;
    }

    private static int ChooseChunkSize(int sampleLength, int targetLength)
    {
        var desired = Math.Max(sampleLength * 2, 1 << 16);
        desired = Math.Min(desired, 1 << 20);
        desired = Math.Min(desired, Math.Max(sampleLength, targetLength - sampleLength + 1));
        return Math.Max(1, desired);
    }
}
