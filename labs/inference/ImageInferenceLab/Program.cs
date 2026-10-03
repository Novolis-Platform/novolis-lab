using Novolis.Inference.Images;
using Novolis.Inference.Safety;

var request = new ImageGenerationRequest("a quiet harbor at dawn");
var policy = new ThresholdImageSafetyClassifier((_, _) => Task.FromResult(0.1f));
var result = await policy.ClassifyAsync(ReadOnlyMemory<byte>.Empty);
Console.WriteLine($"Prompt: {request.Prompt}");
Console.WriteLine($"Size: {request.Width}x{request.Height}");
Console.WriteLine($"Safety allowed: {result.IsAllowed}");
Console.WriteLine("Provide an ONNX model path to exercise a graph-specific image pipeline.");
