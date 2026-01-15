using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Steamworks;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;

namespace SteamUtility.Commands
{
    public class GetUserSummary : ICommand
    {
        public void Execute(string[] args)
        {
            Environment.SetEnvironmentVariable("SteamAppId", "730");

            // Initialize Steam API
            if (!SteamAPI.Init())
            {
                Console.WriteLine("{\"error\":\"Failed to initialize Steam API. The Steam client must be running\"}");
                return;
            }

            try
            {
                CSteamID steamId;
                if (args != null && args.Length > 1 && ulong.TryParse(args[1], out ulong inputSteamId))
                {
                    steamId = new CSteamID(inputSteamId);
                }
                else
                {
                    steamId = SteamUser.GetSteamID();
                }
                string steamIdStr = steamId.ToString();

                // Request user info (name + avatar)
                bool requested = SteamFriends.RequestUserInformation(steamId, false);

                // If info is being requested, poll for up to 2 seconds for avatar to become available
                int tries = 0;
                int avatarInt = SteamFriends.GetLargeFriendAvatar(steamId);
                while (requested && (avatarInt == 0 || avatarInt == -1) && tries < 20)
                {
                    SteamAPI.RunCallbacks();
                    Thread.Sleep(100);
                    avatarInt = SteamFriends.GetLargeFriendAvatar(steamId);
                    tries++;
                }

                // Get persona name (should be available after RequestUserInformation)
                string personaName = SteamFriends.GetFriendPersonaName(steamId);

                string avatarDataUrl = GetAvatarDataUrl(avatarInt);

                var userSummary = new Dictionary<string, object>
                {
                    { "steamid", steamIdStr },
                    { "personaname", personaName },
                    { "avatar", avatarDataUrl }
                };

                var result = new List<Dictionary<string, object>> { userSummary };
                Console.WriteLine(JsonConvert.SerializeObject(result));
            }
            catch (Exception ex)
            {
                Console.WriteLine("{\"error\":\"" + ex.Message + "\"}");
            }
            finally
            {
                SteamAPI.Shutdown();
            }
        }

        // Helper to get the avatar as a base64-encoded PNG data URL
        private string GetAvatarDataUrl(int avatarInt)
        {
            if (avatarInt == 0 || avatarInt == -1)
            {
                // Return empty or a default image if not available
                return "";
            }

            uint width, height;
            if (!SteamUtils.GetImageSize(avatarInt, out width, out height) || width == 0 || height == 0)
            {
                return "";
            }

            byte[] imageData = new byte[4 * width * height];
            if (!SteamUtils.GetImageRGBA(avatarInt, imageData, (int)(4 * width * height)))
            {
                return "";
            }

            // Convert RGBA to BGRA for System.Drawing.Bitmap
            for (int i = 0; i < imageData.Length; i += 4)
            {
                byte r = imageData[i];
                byte b = imageData[i + 2];
                imageData[i] = b;
                imageData[i + 2] = r;
            }

            using (var bmp = new Bitmap((int)width, (int)height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                var data = bmp.LockBits(new Rectangle(0, 0, (int)width, (int)height), ImageLockMode.WriteOnly, bmp.PixelFormat);
                System.Runtime.InteropServices.Marshal.Copy(imageData, 0, data.Scan0, imageData.Length);
                bmp.UnlockBits(data);

                using (var ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    string base64 = Convert.ToBase64String(ms.ToArray());
                    return $"data:image/png;base64,{base64}";
                }
            }
        }
    }
}
