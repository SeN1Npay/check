namespace Metamorph.App.Remote;

internal sealed class BridgePermissions
{
	private volatile bool _commands = true;

	private volatile bool _input = true;

	public bool Commands
	{
		get
		{
			return _commands;
		}
		set
		{
			_commands = value;
		}
	}

	public bool Input
	{
		get
		{
			return _input;
		}
		set
		{
			_input = value;
		}
	}
}
