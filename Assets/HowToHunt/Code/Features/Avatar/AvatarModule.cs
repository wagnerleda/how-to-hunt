using HowToHunt.Avatar;
using HowToHunt.Core;
using HowToHunt.Foundation;
using HowToHunt.Net;

[assembly: FeatureModule(typeof(AvatarModule))]

namespace HowToHunt.Avatar
{
    /// <summary>
    /// The players' look: every hunter is the same stick figure (and a leaf); what tells them apart is the tribe mask
    /// each one paints in the main menu before joining. The mask is saved in config.cfg and sent to the party on join.
    /// </summary>
    public sealed class AvatarModule : IFeatureModule
    {
        public static readonly StringCvar MaskSetting = new StringCvar("cl_mask", string.Empty,
            "Your tribe mask (painted in the main menu: Customize your mask). Empty = the default mask.", CvarFlags.Archive);

        public string Id => "avatar";
        public int Order => 10;

        public void Register(ModuleRegistrar r)
        {
            r.Cvar(MaskSetting);
            r.Command("mask_reset", "Go back to the default tribe mask.", _ => MaskSetting.Value = string.Empty);
        }

        public void Initialize()
        {
            Game.Root.gameObject.AddComponent<MaskEditor>();
            MenuPanels.Add("Customize your mask", MaskEditor.Show);
            MaskSetting.Changed += _ => SendMaskOnJoin();
            SendMaskOnJoin();
        }

        public void Shutdown()
        {
        }

        /// <summary>What the next hello carries (a mask is changed before joining, so that is enough).</summary>
        private static void SendMaskOnJoin()
        {
            var cells = new byte[MaskData.Cells];
            if (!MaskData.FromText(MaskSetting.Value, cells)) cells = MaskData.Default();
            PartyRoster.LocalMask = MaskData.Pack(cells);
        }
    }
}
