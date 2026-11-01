// U.RICO-T146: an automated stand-in for the manual room-by-room pass U.RICO-T58 did by eye. Launched with -autowalk,
// Game scripts itself through every room (Game.RunAutowalk) and this class only saves what each one looks like: a
// numbered screenshot per room under Logs/autowalk/, so a later run's images can be compared against the original's
// rooms (R:\...\gmlRicochet) without anyone at the keyboard. Opt-in only: an ordinary launch never touches this file.
using System;
using System.IO;
using UnityEngine;

namespace Ricochet
{
    public static class Autowalk
    {
        private static int _step;

        /// <summary>True when the process was launched with -autowalk (e.g. <c>Tools/play.ps1 -PlayerArgs -autowalk</c>).</summary>
        public static bool Requested { get; } = Array.Exists(Environment.GetCommandLineArgs(), a => a == "-autowalk");

        /// <summary>Where this run's screenshots land: Logs/autowalk beside the player's own Logs folder.</summary>
        public static string Folder { get; } = Path.Combine(Application.dataPath, "..", "Logs", "autowalk");

        /// <summary>
        /// Saves a numbered screenshot named for <paramref name="room"/> (00-splash.png, 01-title.png, ...). A no-op
        /// unless <see cref="Requested"/>, so calling it from Game costs nothing on an ordinary launch.
        /// </summary>
        public static void Capture(string room)
        {
            if (!Requested)
            {
                return;
            }

            Directory.CreateDirectory(Folder);
            string path = Path.Combine(Folder, _step.ToString("00") + "-" + room + ".png");
            _step++;
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[Autowalk] " + path);
        }
    }
}
