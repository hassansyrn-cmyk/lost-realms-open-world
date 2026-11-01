using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LostRealmsOW {
	public static class AnimInspector {
		public static void Inspect() {
			var guids = AssetDatabase.FindAssets("t:AnimationClip");
			Debug.Log($"[AnimInspector] Total AnimationClips found in project: {guids.Length}");
			string[] keywords = new string[] { "open", "chest", "reach", "loot", "interact", "take", "pickup", "gather", "use", "action", "dialogue", "talk", "cast" };
			foreach (var guid in guids) {
				string path = AssetDatabase.GUIDToAssetPath(guid);
				string lower = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
				if (keywords.Any(k => lower.Contains(k))) {
					var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
					if (clip != null) {
						Debug.Log($"[MATCH] name='{clip.name}' length={clip.length:F2}s isLoop={clip.isLooping} human={clip.humanMotion} path={path}");
					}
				}
			}
		}
	}
}
