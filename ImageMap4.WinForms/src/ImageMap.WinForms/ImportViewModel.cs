using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing.Drawing2D;
using ImageMap.WinForms;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Dithering;
using SixLabors.ImageSharp.Processing.Processors.Transforms;

namespace ImageMap4
{
    public class ImportViewModel : ObservableObject
    {
        public event EventHandler? OnClosed;
        public event EventHandler<IList<ImportSettings>>? OnConfirmed;

        private bool _hadMultiple;
        public bool HadMultiple
        {
            get => _hadMultiple;
            set { _hadMultiple = value; OnPropertyChanged(); }
        }

        private bool _javaMode;
        public bool JavaMode
        {
            get => _javaMode;
            set { _javaMode = value; OnPropertyChanged(); }
        }

        private int _gridWidth = 1;
        public int GridWidth
        {
            get => _gridWidth;
            set { _gridWidth = Math.Max(1, Math.Min(100, value)); OnPropertyChanged(); }
        }

        private int _gridHeight = 1;
        public int GridHeight
        {
            get => _gridHeight;
            set { _gridHeight = Math.Max(1, Math.Min(100, value)); OnPropertyChanged(); }
        }

        public record StretchOption(string Name, ResizeMode Mode);
        public StretchOption StretchChoice
        {
            get => StretchOptions[Clamp(AppSettings.Default.StretchChoice, StretchOptions.Count)];
            set { AppSettings.Default.StretchChoice = StretchOptions.IndexOf(value); OnPropertyChanged(); }
        }
        public ReadOnlyCollection<StretchOption> StretchOptions { get; } = new List<StretchOption>
        {
            new("Uniform", ResizeMode.Max),
            new("Stretch", ResizeMode.Stretch),
            new("Crop", ResizeMode.Crop)
        }.AsReadOnly();

        public record ScalingOption(string Name, Func<SixLabors.ImageSharp.Size, InterpolationMode> Mode, Func<SixLabors.ImageSharp.Size, IResampler> Sampler);
        public ScalingOption ScaleChoice
        {
            get => ScaleOptions[Clamp(AppSettings.Default.ScaleChoice, ScaleOptions.Count)];
            set
            {
                AppSettings.Default.ScaleChoice = ScaleOptions.IndexOf(value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentMode));
            }
        }
        public ReadOnlyCollection<ScalingOption> ScaleOptions { get; } = new List<ScalingOption>
        {
            new("Automatic",
                x => x.Width > 128 && x.Height > 128 ? InterpolationMode.HighQualityBicubic : InterpolationMode.NearestNeighbor,
                x => x.Width > 128 && x.Height > 128 ? KnownResamplers.Bicubic : KnownResamplers.NearestNeighbor),
            new("Pixel Art", _ => InterpolationMode.NearestNeighbor, _ => KnownResamplers.NearestNeighbor),
            new("Bicubic", _ => InterpolationMode.HighQualityBicubic, _ => KnownResamplers.Bicubic)
        }.AsReadOnly();
        public InterpolationMode CurrentMode => CurrentImage == null
            ? InterpolationMode.NearestNeighbor
            : ScaleChoice.Mode(ImageSize(CurrentImage.Source.Image.Value));

        public record DitherOption(string Name, IDither? Dither);
        public DitherOption DitherChoice
        {
            get => DitherOptions[Clamp(AppSettings.Default.DitherChoice, DitherOptions.Count)];
            set { AppSettings.Default.DitherChoice = DitherOptions.IndexOf(value); OnPropertyChanged(); }
        }
        public ReadOnlyCollection<DitherOption> DitherOptions { get; } = new List<DitherOption>
        {
            new("None", null),
            new("Floyd Steinberg", KnownDitherings.FloydSteinberg),
            new("Burks", KnownDitherings.Burks)
        }.AsReadOnly();

        public record AlgorithmOption(string Name, IColorAlgorithm Algorithm);
        public AlgorithmOption AlgorithmChoice
        {
            get => AlgorithmOptions[Clamp(AppSettings.Default.AlgorithmChoice, AlgorithmOptions.Count)];
            set { AppSettings.Default.AlgorithmChoice = AlgorithmOptions.IndexOf(value); OnPropertyChanged(); }
        }
        public ReadOnlyCollection<AlgorithmOption> AlgorithmOptions { get; } = new List<AlgorithmOption>
        {
            new("Good Fast", new SimpleAlgorithm()),
            new("Euclidean", new EuclideanAlgorithm()),
            new("CIEDE2000", new Ciede2000Algorithm()),
            new("CIE76", new Cie76Algorithm()),
            new("CMC", new CmcAlgorithm()),
            new("Oklab", new OkLabAlgorithm())
        }.AsReadOnly();

        public record BackgroundColorOption(System.Drawing.Color Color, Rgba32 Pixel);
        public BackgroundColorOption BackgroundColorChoice
        {
            get => BackgroundColorOptions[Clamp(AppSettings.Default.BackgroundColorChoice, BackgroundColorOptions.Count)];
            set { AppSettings.Default.BackgroundColorChoice = BackgroundColorOptions.IndexOf(value); OnPropertyChanged(); }
        }
        public ReadOnlyCollection<BackgroundColorOption> BackgroundColorOptions { get; } = new List<BackgroundColorOption>
        {
            new(System.Drawing.Color.Transparent, SixLabors.ImageSharp.Color.Transparent),
            new(System.Drawing.Color.White, SixLabors.ImageSharp.Color.White),
            new(System.Drawing.Color.Black, SixLabors.ImageSharp.Color.Black)
        }.AsReadOnly();

        public ObservableCollection<PreviewImage> ImageQueue { get; } = new();
        private int _currentIndex;
        public PreviewImage? CurrentImage => ImageQueue.Count == 0 ? null : ImageQueue[_currentIndex];

        public void Rotate(float degrees)
        {
            if (CurrentImage == null)
                return;
            CurrentImage.Rotation = (CurrentImage.Rotation + degrees * Math.Sign(CurrentImage.ScaleX * CurrentImage.ScaleY)) % 360;
        }

        public void FlipHorizontal()
        {
            if (CurrentImage != null)
                CurrentImage.ScaleX *= -1;
        }

        public void FlipVertical()
        {
            if (CurrentImage != null)
                CurrentImage.ScaleY *= -1;
        }

        public void SwitchImage(PreviewImage preview)
        {
            int index = ImageQueue.IndexOf(preview);
            if (index < 0)
                return;
            _currentIndex = index;
            OnPropertyChanged(nameof(CurrentImage));
            OnPropertyChanged(nameof(CurrentMode));
        }

        public void Discard()
        {
            if (ImageQueue.Count == 0)
                return;
            ImageQueue.RemoveAt(_currentIndex);
            if (_currentIndex >= ImageQueue.Count)
                _currentIndex--;
            OnPropertyChanged(nameof(CurrentImage));
            OnPropertyChanged(nameof(CurrentMode));
            CloseIfDone();
        }

        public void DiscardAll()
        {
            ImageQueue.Clear();
            _currentIndex = 0;
            OnPropertyChanged(nameof(CurrentImage));
            OnPropertyChanged(nameof(CurrentMode));
            CloseIfDone();
        }

        public void Confirm()
        {
            if (CurrentImage != null)
                ConfirmImages(new[] { CurrentImage });
            if (ImageQueue.Count == 0)
                return;
            ImageQueue.RemoveAt(_currentIndex);
            if (_currentIndex >= ImageQueue.Count)
                _currentIndex--;
            OnPropertyChanged(nameof(CurrentImage));
            OnPropertyChanged(nameof(CurrentMode));
            CloseIfDone();
        }

        public void ConfirmAll()
        {
            ConfirmImages(ImageQueue);
            ImageQueue.Clear();
            _currentIndex = 0;
            OnPropertyChanged(nameof(CurrentImage));
            OnPropertyChanged(nameof(CurrentMode));
            CloseIfDone();
        }

        public void Navigate(int delta)
        {
            if (ImageQueue.Count == 0)
                return;
            _currentIndex = ((_currentIndex + delta) % ImageQueue.Count + ImageQueue.Count) % ImageQueue.Count;
            OnPropertyChanged(nameof(CurrentImage));
            OnPropertyChanged(nameof(CurrentMode));
        }

        public void ChangeBackground()
        {
            int next = (BackgroundColorOptions.IndexOf(BackgroundColorChoice) + 1) % BackgroundColorOptions.Count;
            BackgroundColorChoice = BackgroundColorOptions[next];
        }

        private void ConfirmImages(IEnumerable<PreviewImage> previews)
        {
            var settings = new List<ImportSettings>();
            foreach (var preview in previews)
            {
                settings.Add(new ImportSettings(
                    preview, GridWidth, GridHeight,
                    new Lazy<IResampler>(() => ScaleChoice.Sampler(ImageSize(preview.Source.Image.Value))),
                    StretchChoice.Mode, BackgroundColorChoice.Pixel,
                    new ProcessSettings(DitherChoice.Dither, AlgorithmChoice.Algorithm)));
            }
            OnConfirmed?.Invoke(this, settings);
        }

        private void CloseIfDone()
        {
            if (ImageQueue.Count == 0)
                OnClosed?.Invoke(this, EventArgs.Empty);
        }

        public void AddImages(IEnumerable<PendingSource> sources)
        {
            foreach (var source in sources)
                ImageQueue.Add(new PreviewImage(source));
            OnPropertyChanged(nameof(CurrentImage));
            OnPropertyChanged(nameof(CurrentMode));
            HadMultiple = ImageQueue.Count > 1;
        }

        private static int Clamp(int value, int count) => count == 0 ? 0 : Math.Max(0, Math.Min(count - 1, value));

        private static SixLabors.ImageSharp.Size ImageSize(Image<Rgba32> image) => new(image.Width, image.Height);
    }
}
