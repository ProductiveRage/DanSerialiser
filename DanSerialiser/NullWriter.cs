namespace DanSerialiser
{
	public sealed class NullWriter : IWrite
	{
		public static NullWriter Instance { get; } = new NullWriter();
		private NullWriter() { }

		public void Dispose() { }
	}
}
