// Copyright (c) 2023 EchKode
// SPDX-License-Identifier: BSD-3-Clause

using System.IO;

using UnityEngine;

namespace EchKode.PBMods.TargetedUnitPopup
{
	partial class ModLink
	{
		internal sealed class ModSettings
		{
			[System.Flags]
			internal enum LoggingFlag
			{
				None = 0,
				System = 0x1,
				ActionHook = 0x2,
				OnHover = 0x4,
				All = 0xFF,
			}

#pragma warning disable CS0649
			public LoggingFlag logging;
			public bool showPopupsForShields;
			public bool showOnDrag;
#pragma warning restore CS0649

			internal bool IsLoggingEnabled(LoggingFlag flag) => (logging & flag) == flag;
		}

		internal static ModSettings Settings;

		internal static void LoadSettings()
		{
			var settingsPath = Path.Combine(modPath, "settings.yaml");
			Settings = UtilitiesYAML.ReadFromFile<ModSettings>(settingsPath, false);
			if (Settings == null)
			{
				Settings = new ModSettings();

				Debug.LogFormat(
					"Mod {0} ({1}) no settings file found, using defaults | path: {2}",
					modIndex,
					modID,
					settingsPath);
			}
		}
	}
}
