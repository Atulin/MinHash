namespace MinHash;

public sealed class EnumOutOfRangeException<T>(T value) : Exception where T : Enum
{
	public override string Message => $"Enum value {value} is out of range: {typeof(T).Name}";
}