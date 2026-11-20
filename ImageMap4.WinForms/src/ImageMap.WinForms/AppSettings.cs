using System.Collections.Specialized;
using System.Configuration;

namespace ImageMap.WinForms
{
    internal sealed class AppSettings : ApplicationSettingsBase
    {
        public static AppSettings Default { get; } = (AppSettings)Synchronized(new AppSettings());

        [UserScopedSetting]
        [DefaultSettingValue("0")]
        public int StretchChoice
        {
            get => (int)this[nameof(StretchChoice)];
            set => this[nameof(StretchChoice)] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("0")]
        public int ScaleChoice
        {
            get => (int)this[nameof(ScaleChoice)];
            set => this[nameof(ScaleChoice)] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("0")]
        public int DitherChoice
        {
            get => (int)this[nameof(DitherChoice)];
            set => this[nameof(DitherChoice)] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("0")]
        public int AlgorithmChoice
        {
            get => (int)this[nameof(AlgorithmChoice)];
            set => this[nameof(AlgorithmChoice)] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("False")]
        public bool ShowEmptyMaps
        {
            get => (bool)this[nameof(ShowEmptyMaps)];
            set => this[nameof(ShowEmptyMaps)] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("0")]
        public int BackgroundColorChoice
        {
            get => (int)this[nameof(BackgroundColorChoice)];
            set => this[nameof(BackgroundColorChoice)] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("False")]
        public bool InvisibleFrames
        {
            get => (bool)this[nameof(InvisibleFrames)];
            set => this[nameof(InvisibleFrames)] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("False")]
        public bool GlowingFrames
        {
            get => (bool)this[nameof(GlowingFrames)];
            set => this[nameof(GlowingFrames)] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("True")]
        public bool CreateMaps
        {
            get => (bool)this[nameof(CreateMaps)];
            set => this[nameof(CreateMaps)] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("True")]
        public bool CreateStructures
        {
            get => (bool)this[nameof(CreateStructures)];
            set => this[nameof(CreateStructures)] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("1")]
        public int InventoryChoice
        {
            get => (int)this[nameof(InventoryChoice)];
            set => this[nameof(InventoryChoice)] = value;
        }

        [UserScopedSetting]
        public StringCollection UsernameCache
        {
            get => (StringCollection)this[nameof(UsernameCache)];
            set => this[nameof(UsernameCache)] = value;
        }

        [UserScopedSetting]
        public StringCollection JavaFolders
        {
            get => (StringCollection)this[nameof(JavaFolders)];
            set => this[nameof(JavaFolders)] = value;
        }

        [UserScopedSetting]
        public StringCollection BedrockFolders
        {
            get => (StringCollection)this[nameof(BedrockFolders)];
            set => this[nameof(BedrockFolders)] = value;
        }
    }
}
