using RoR2;

namespace DroneImprovements
{
    internal static class DroneUtil
    {
        /// <summary>A body that a player is remote-operating (the vanilla "Remote Operation" drone).</summary>
        public static bool IsDronePlayer(CharacterBody body)
        {
            return body && body.isPlayerControlled && body.isRemoteOp;
        }

        /// <summary>Players that currently have a real (non-drone) body.</summary>
        public static int CountLivingNonDronePlayers()
        {
            int count = 0;
            foreach (PlayerCharacterMasterController pcmc in PlayerCharacterMasterController.instances)
            {
                if (!pcmc || !pcmc.master)
                {
                    continue;
                }
                CharacterBody body = pcmc.master.GetBody();
                if (body && !body.isRemoteOp)
                {
                    count++;
                }
            }
            return count;
        }
    }
}
