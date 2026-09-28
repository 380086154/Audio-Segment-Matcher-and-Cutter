using AudioMatcher.Core.Matching;
using AudioMatcher.Core.Models;

namespace AudioMatcher.Tests.Matching;

public sealed class AudioMonoResamplerTests
{
    [Fact]
    public void Stereo_IsAveragedToMono()
    {
        var stereo = new PcmAudio([0.2f, 0.8f, 1f, 1f], 8000, 2);
        var mono = AudioMonoResampler.ToMono(stereo);
        Assert.Equal(2, mono.Length);
        Assert.Equal(0.5f, mono[0], 3);
        Assert.Equal(1f, mono[1], 3);
    }

    [Fact]
    public void Downsample_ReducesLength()
    {
        var input = Enumerable.Range(0, 8000).Select(i => (float)Math.Sin(i)).ToArray();
        var output = AudioMonoResampler.ResampleLinear(input, 8000, 4000);
        Assert.InRange(output.Length, 3990, 4010);
    }
}
