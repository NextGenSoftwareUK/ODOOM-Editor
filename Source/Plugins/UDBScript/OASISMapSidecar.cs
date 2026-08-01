#region ================== OASIS Map Sidecar

/*
 * Per-map sidecar file: oasis_{mapname}.json
 *
 * Stores cross-game metadata that cannot live inside a standard Doom WAD or Quake .map:
 *   - OASIS portals: destination game / map / coordinates
 *   - Cross-game entity placements: which OASIS thing type came from which source game
 *
 * Written by OASISPortalPanel when a portal is placed.
 * Read by the OASIS Omniverse kernel at runtime to resolve portal destinations.
 *
 * JSON format (hand-serialised to avoid external library dependency):
 * {
 *   "oasisVersion": "1.0",
 *   "portals": [
 *     {
 *       "thingId": 0,
 *       "x": 64, "y": -128,
 *       "destinationGame": "OQUAKE2",
 *       "destinationMap": "q2dm1",
 *       "destinationX": 0, "destinationY": 0, "destinationZ": 0
 *     }
 *   ],
 *   "crossGameEntities": [
 *     {
 *       "thingId": 0,
 *       "thingType": 6001,
 *       "sourceGame": "OQUAKE2",
 *       "displayName": "Blue Keycard (Q2)"
 *     }
 *   ]
 * }
 */

#endregion

#region ================== Namespaces

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

#endregion

namespace CodeImp.DoomBuilder.UDBScript
{
	public static class OASISMapSidecar
	{
		/// <summary>Returns the sidecar path for the currently open map, or null if no map is open.</summary>
		public static string GetSidecarPath()
		{
			if (General.Map == null) return null;
			string mapPath = General.Map.FilePathName;
			if (string.IsNullOrEmpty(mapPath)) return null;
			string dir = Path.GetDirectoryName(mapPath) ?? "";
			string baseName = Path.GetFileNameWithoutExtension(mapPath);
			return Path.Combine(dir, "oasis_" + baseName + ".json");
		}

		/// <summary>Appends a portal entry to the sidecar (creates file if it does not exist).</summary>
		public static void AppendPortal(PortalEntry portal)
		{
			string path = GetSidecarPath();
			if (path == null) return;

			SidecarData data = File.Exists(path) ? Load(path) : new SidecarData();
			data.Portals.Add(portal);
			Save(path, data);
		}

		/// <summary>Appends a cross-game entity entry to the sidecar.</summary>
		public static void AppendCrossGameEntity(CrossGameEntityEntry entry)
		{
			string path = GetSidecarPath();
			if (path == null) return;

			SidecarData data = File.Exists(path) ? Load(path) : new SidecarData();
			data.CrossGameEntities.Add(entry);
			Save(path, data);
		}

		/// <summary>Returns the full sidecar path for display, or empty string if unavailable.</summary>
		public static string GetSidecarPathForDisplay()
		{
			return GetSidecarPath() ?? string.Empty;
		}

		// ── Serialisation ─────────────────────────────────────────────────────────

		private static SidecarData Load(string path)
		{
			try
			{
				string json = File.ReadAllText(path, Encoding.UTF8);
				return ParseSidecar(json);
			}
			catch
			{
				return new SidecarData();
			}
		}

		private static void Save(string path, SidecarData data)
		{
			File.WriteAllText(path, Serialise(data), Encoding.UTF8);
		}

		private static string Serialise(SidecarData data)
		{
			var sb = new StringBuilder();
			sb.AppendLine("{");
			sb.AppendLine("  \"oasisVersion\": \"1.0\",");

			sb.AppendLine("  \"portals\": [");
			for (int i = 0; i < data.Portals.Count; i++)
			{
				var p = data.Portals[i];
				sb.Append("    {");
				sb.Append(" \"thingId\": " + p.ThingId + ",");
				sb.Append(" \"x\": " + p.X + ", \"y\": " + p.Y + ",");
				sb.Append(" \"destinationGame\": \"" + Escape(p.DestinationGame) + "\",");
				sb.Append(" \"destinationMap\": \"" + Escape(p.DestinationMap) + "\",");
				sb.Append(" \"destinationX\": " + p.DestinationX + ",");
				sb.Append(" \"destinationY\": " + p.DestinationY + ",");
				sb.Append(" \"destinationZ\": " + p.DestinationZ);
				sb.Append(" }");
				if (i < data.Portals.Count - 1) sb.Append(",");
				sb.AppendLine();
			}
			sb.AppendLine("  ],");

			sb.AppendLine("  \"crossGameEntities\": [");
			for (int i = 0; i < data.CrossGameEntities.Count; i++)
			{
				var e = data.CrossGameEntities[i];
				sb.Append("    {");
				sb.Append(" \"thingId\": " + e.ThingId + ",");
				sb.Append(" \"thingType\": " + e.ThingType + ",");
				sb.Append(" \"sourceGame\": \"" + Escape(e.SourceGame) + "\",");
				sb.Append(" \"displayName\": \"" + Escape(e.DisplayName) + "\"");
				sb.Append(" }");
				if (i < data.CrossGameEntities.Count - 1) sb.Append(",");
				sb.AppendLine();
			}
			sb.AppendLine("  ]");
			sb.Append("}");
			return sb.ToString();
		}

		private static string Escape(string s)
		{
			if (string.IsNullOrEmpty(s)) return string.Empty;
			return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
		}

		// ── Minimal JSON parser (portals + crossGameEntities arrays only) ─────────

		private static SidecarData ParseSidecar(string json)
		{
			var data = new SidecarData();

			string portalsJson = ExtractArray(json, "portals");
			if (!string.IsNullOrEmpty(portalsJson))
				foreach (string obj in ExtractObjects(portalsJson))
					data.Portals.Add(ParsePortal(obj));

			string entitiesJson = ExtractArray(json, "crossGameEntities");
			if (!string.IsNullOrEmpty(entitiesJson))
				foreach (string obj in ExtractObjects(entitiesJson))
					data.CrossGameEntities.Add(ParseEntity(obj));

			return data;
		}

		private static string ExtractArray(string json, string key)
		{
			var m = Regex.Match(json, "\"" + Regex.Escape(key) + @"""\s*:\s*\[([^\[\]]*(?:\[[^\[\]]*\][^\[\]]*)*)\]", RegexOptions.Singleline);
			return m.Success ? m.Groups[1].Value : null;
		}

		private static IEnumerable<string> ExtractObjects(string arrayContent)
		{
			var results = new List<string>();
			int depth = 0, start = -1;
			for (int i = 0; i < arrayContent.Length; i++)
			{
				if (arrayContent[i] == '{') { if (depth++ == 0) start = i; }
				else if (arrayContent[i] == '}') { if (--depth == 0 && start >= 0) { results.Add(arrayContent.Substring(start, i - start + 1)); start = -1; } }
			}
			return results;
		}

		private static PortalEntry ParsePortal(string obj)
		{
			return new PortalEntry
			{
				ThingId         = ParseInt(obj, "thingId"),
				X               = ParseDouble(obj, "x"),
				Y               = ParseDouble(obj, "y"),
				DestinationGame = ParseString(obj, "destinationGame"),
				DestinationMap  = ParseString(obj, "destinationMap"),
				DestinationX    = ParseDouble(obj, "destinationX"),
				DestinationY    = ParseDouble(obj, "destinationY"),
				DestinationZ    = ParseDouble(obj, "destinationZ"),
			};
		}

		private static CrossGameEntityEntry ParseEntity(string obj)
		{
			return new CrossGameEntityEntry
			{
				ThingId     = ParseInt(obj, "thingId"),
				ThingType   = ParseInt(obj, "thingType"),
				SourceGame  = ParseString(obj, "sourceGame"),
				DisplayName = ParseString(obj, "displayName"),
			};
		}

		private static int ParseInt(string obj, string key)
		{
			var m = Regex.Match(obj, "\"" + Regex.Escape(key) + @"""\s*:\s*(-?\d+)");
			return m.Success && int.TryParse(m.Groups[1].Value, out int v) ? v : 0;
		}

		private static double ParseDouble(string obj, string key)
		{
			var m = Regex.Match(obj, "\"" + Regex.Escape(key) + @"""\s*:\s*(-?[\d.]+)");
			return m.Success && double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double v) ? v : 0.0;
		}

		private static string ParseString(string obj, string key)
		{
			var m = Regex.Match(obj, "\"" + Regex.Escape(key) + @"""\s*:\s*""([^""]*)""");
			return m.Success ? m.Groups[1].Value.Replace("\\\"", "\"").Replace("\\\\", "\\") : string.Empty;
		}

		// ── Data models ───────────────────────────────────────────────────────────

		private class SidecarData
		{
			public List<PortalEntry> Portals = new List<PortalEntry>();
			public List<CrossGameEntityEntry> CrossGameEntities = new List<CrossGameEntityEntry>();
		}

		public class PortalEntry
		{
			public int    ThingId;
			public double X, Y;
			public string DestinationGame;
			public string DestinationMap;
			public double DestinationX, DestinationY, DestinationZ;
		}

		public class CrossGameEntityEntry
		{
			public int    ThingId;
			public int    ThingType;
			public string SourceGame;
			public string DisplayName;
		}
	}
}
