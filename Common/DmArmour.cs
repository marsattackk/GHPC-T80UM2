using System.Collections.Generic;
using System.Globalization;

namespace T80UM2.Common
{
	public sealed class DmArmour
	{
		public sealed class Entry
		{
			public string Piece, Mode, Name, Material = "", Xray = "", Source = "";
			public float Ke, Ce, KeAfter, CeAfter;
			public bool AngleMatters, Slat;

			public bool Variable => Mode == "geometry";
			public bool Era => Mode == "era";
			public bool Convex => Mode == "composite";
			public bool Drawn => Mode == "drawn";
		}

		public const string AssetName = "dm_armour";
		public static readonly string[] Materials = { "armour_steel", "mild_steel", "plain", "composite", "filling", "filling_steel", "liner" };

		public readonly List<Entry> Entries = new List<Entry>();
		readonly Dictionary<string, Entry> _byPiece = new Dictionary<string, Entry>();

		public Entry this[string piece] => piece != null && _byPiece.TryGetValue(piece, out var e) ? e : null;

		public void Add(Entry e) { Entries.Add(e); _byPiece[e.Piece] = e; }

		static readonly CultureInfo CI = CultureInfo.InvariantCulture;

		public static DmArmour Parse(string text)
		{
			var d = new DmArmour();
			foreach (var raw in text.Split('\n'))
			{
				var line = raw.TrimEnd('\r');
				if (line.Length == 0 || line.StartsWith("#")) continue;
				var f = line.Split('\t');
				if (f.Length < 11) continue;   // older bundles stop at source, no slat column
				d.Add(new Entry
				{
					Piece = f[0], Mode = f[1], Ke = float.Parse(f[2], CI), Ce = float.Parse(f[3], CI), KeAfter = float.Parse(f[4], CI), CeAfter = float.Parse(f[5], CI),
					AngleMatters = f[6] == "1", Material = f[7], Xray = f[8], Name = f[9], Source = f[10], Slat = f.Length > 11 && f[11] == "1",
				});
			}
			return d;
		}
	}
}
