namespace MinHash;

public enum ShingleMode
{
	/// <summary>
	/// Shingle size will be based on characters
	/// </summary>
	/// <example>
	///	text: "hello world"
	/// shingle size: 5
	/// ["hello", "ello ", "llo w", "lo wo", "o wor", " worl", "world"]
	/// </example>
	Chars,
	
	/// <summary>
	/// Shingle size will be based on words
	/// </summary>
	/// <example>
	/// text: "sphinx of black quartz, judge my vow"
	/// shingle size: 3
	/// ["sphinx of black", "of black quartz", "black quartz judge", "quartz judge my", "judge my vow"]
	/// </example>
	Words,
}