namespace Metamorph.Core.Games;

// Рандомайзер «7-bag»: каждые семь фигур — перестановка всех семи.
public sealed class SevenBag
{
	private readonly GameRandom _rnd;

	private readonly TetrominoKind[] _bag = new TetrominoKind[Tetromino.Count];

	private int _index = Tetromino.Count;

	public SevenBag(GameRandom rnd)
	{
		_rnd = rnd;
	}

	public TetrominoKind Next()
	{
		if (_index >= _bag.Length)
		{
			for (int i = 0; i < _bag.Length; i++)
			{
				_bag[i] = (TetrominoKind)i;
			}
			for (int i = _bag.Length - 1; i > 0; i--)
			{
				int j = _rnd.Next(i + 1);
				(_bag[i], _bag[j]) = (_bag[j], _bag[i]);
			}
			_index = 0;
		}
		return _bag[_index++];
	}
}
