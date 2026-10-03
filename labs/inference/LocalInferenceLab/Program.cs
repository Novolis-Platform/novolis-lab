using Novolis.Inference.Ollama;

var options = new OllamaChatModelOptions { Model = args.FirstOrDefault() ?? "llama3.2" };
Console.WriteLine($"Provider: Ollama");
Console.WriteLine($"Endpoint: {options.Endpoint}");
Console.WriteLine($"Model: {options.Model}");
Console.WriteLine("Pass a running Ollama endpoint to exercise the full chat request.");
