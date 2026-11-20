namespace VehicleGo.UnitTests.Fakes;

/// <summary>Ordered record of calls made to the fakes, used to assert the flow order of handlers.</summary>
public sealed class CallLog
{
    private readonly List<string> _calls = [];

    public IReadOnlyList<string> Calls => _calls;

    public void Add(string call) => _calls.Add(call);

    public void Clear() => _calls.Clear();
}
