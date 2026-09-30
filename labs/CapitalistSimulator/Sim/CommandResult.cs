namespace CapitalistSimulator.Sim;

internal sealed record CommandResult(bool Ok, string Message)
{
    public static CommandResult Success(string message = "OK") => new(true, message);
    public static CommandResult Fail(string message) => new(false, message);
}
