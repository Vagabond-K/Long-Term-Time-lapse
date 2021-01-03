using Accord.Genetic;
using Accord.Math.Random;
using LongTermTimeLapse.TimeLapseFrameProcessors;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace LongTermTimeLapse
{
    public class TimeLapse : NotifyPropertyChangeObject
    {
        static TimeLapse()
        {
            CurrentDomain_DomainUnload(null, null);
            AppDomain.CurrentDomain.DomainUnload += CurrentDomain_DomainUnload;
        }

        private static void CurrentDomain_DomainUnload(object sender, EventArgs e)
        {
            if (Directory.Exists(PreProcessedFilePath))
                Directory.GetFiles(ResizedOriginalFilePath).AsParallel().ForAll(file => File.Delete(file));
            if (Directory.Exists(ResizedOriginalFilePath))
                Directory.GetFiles(ResizedOriginalFilePath).AsParallel().ForAll(file => File.Delete(file));
            if (Directory.Exists(ResizedAdjustedFilePath))
                Directory.GetFiles(ResizedAdjustedFilePath).AsParallel().ForAll(file => File.Delete(file));
            if (Directory.Exists(AdjustedFilePath))
                Directory.GetFiles(AdjustedFilePath).AsParallel().ForAll(file => File.Delete(file));
        }

        public static string PreProcessedFilePath { get => Path.Combine(Path.GetTempPath(), "LongTermTimeLapse", "PreProcessed"); }
        public static string ResizedOriginalFilePath { get => Path.Combine(Path.GetTempPath(), "LongTermTimeLapse", "ResizedOriginal"); }
        public static string ResizedAdjustedFilePath { get => Path.Combine(Path.GetTempPath(), "LongTermTimeLapse", "ResizedAdjusted"); }
        public static string AdjustedFilePath { get => Path.Combine(Path.GetTempPath(), "LongTermTimeLapse", "Adjusted"); }

        public TimeLapse()
        {
            //if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            //{
            //    AddFrame(null);
            //}

            Frames = new ReadOnlyObservableCollection<TimeLapseFrame>(frames);
        }

        private readonly ObservableCollection<TimeLapseFrame> frames = new ObservableCollection<TimeLapseFrame>();

        public ReadOnlyObservableCollection<TimeLapseFrame> Frames { get; }

        private TimeLapseFrame oldCurrentFrame = null;
        public TimeLapseFrame CurrentFrame { get => Get<TimeLapseFrame>(); set { if (Set(value)) CurrentFrameChanged?.Invoke(this, EventArgs.Empty); } }
        public event EventHandler CurrentFrameChanged;

        public TimeLapseFrame AddFrame(string filePath)
        {
            var frame = new TimeLapseFrame(this, filePath);
            frames.Add(frame);
            return frame;
        }

        public Rect TotalRect { get => Get<Rect>(); internal set => Set(value); }
        public Rect ValidRect { get => Get<Rect>(); private set { if (Set(value) && !value.IsEmpty) UpdatedValidRect = true; } }
        public bool UpdatedValidRect { get => Get(false); private set => Set(value); }

        public bool IsAdjusting { get => Get(false); private set => Set(value); }
        public int AdjustComplated { get => Get(0); private set => Set(value); }
        public bool IsOptimizing { get => Get(false); private set => Set(value); }
        public string AdjustProcessMessage { get => IsOptimizing ? "가용 영역 최적화 중..." : $"사진 정렬 중... ({AdjustComplated}/{Frames.Count})"; }

        protected override bool OnPropertyChanging(QueryPropertyChangingEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(CurrentFrame):
                    oldCurrentFrame = CurrentFrame;
                    break;
            }
            return base.OnPropertyChanging(e);
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            switch (e.PropertyName)
            {
                case nameof(AdjustComplated):
                case nameof(IsOptimizing):
                    OnPropertyChanged(new PropertyChangedEventArgs(nameof(AdjustProcessMessage)));
                    break;
                case nameof(CurrentFrame):
                    oldCurrentFrame?.RefreshImage();
                    break;
            }
        }

        public ITimeLapseFrameProcessor TimeLapseFrameProcessor { get => Get<ITimeLapseFrameProcessor>(); set => Set(value); }

        public async Task AdjustAll()
        {
            if (Frames.Count >= 2)
            {
                ValidRect = Rect.Empty;
                IsOptimizing = false;
                IsAdjusting = true;

                AdjustComplated = 0;

                var baseFrame = Frames.FirstOrDefault();

                using (var baseBitmap = baseFrame.GetPreProcessedBitmap())
                {
                    AdjustComplated += 1;
                    CurrentFrame = baseFrame;
                    foreach (var targetFrame in Frames.Skip(1))
                    {
                        await targetFrame.Adjust(baseBitmap, baseFrame.OriginalSize);
                        CurrentFrame = targetFrame;
                        AdjustComplated += 1;
                    }
                }

                IsOptimizing = true;
                await Task.Run(() =>
                {
                    Geometry totalGeometry = null;

                    foreach (var frame in Frames)
                    {
                        PathGeometry geometry = new PathGeometry();
                        geometry.Figures.Add(new PathFigure(
                            new System.Windows.Point(frame.Corners[0].X, frame.Corners[0].Y),
                            frame.Corners.Skip(1).Select(point =>
                            new LineSegment(new System.Windows.Point(point.X, point.Y), true)
                        ), true));

                        if (totalGeometry == null)
                            totalGeometry = geometry;
                        else
                            totalGeometry = Geometry.Combine(totalGeometry, geometry, GeometryCombineMode.Intersect, null);
                    }


                    var population = new Population(1000, new ValidSizeChromosome(totalGeometry),
                        new ValidSizeFitnessFunction(), new RankSelection())
                    {

                    };

                    ushort[] lastValues = null;
                    int equalsCount = 0;
                    for (int i = 0; i < 1000; i++)
                    {
                        population.RunEpoch();
                        if (lastValues != null
                            && lastValues.SequenceEqual((population.BestChromosome as ShortArrayChromosome).Value))
                        {
                            equalsCount++;
                        }
                        else
                        {
                            equalsCount = 0;
                        }

                        if (equalsCount > 10)
                        {
                            break;
                        }
                        lastValues = (population.BestChromosome as ShortArrayChromosome).Value.Select(v => v).ToArray();
                    }

                    var rectValues = (population.BestChromosome as ValidSizeChromosome).Value;
                    ValidRect = new Rect(rectValues[0], rectValues[2], rectValues[1] - rectValues[0], rectValues[3] - rectValues[2]);
                });

                IsAdjusting = false;
            }
        }



        class ValidSizeChromosome : ShortArrayChromosome
        {
            public ValidSizeChromosome(Geometry limitGeometry) : base(4)
            {
                LimitGeometry = limitGeometry;
                Generate();
            }

            public ValidSizeChromosome(ValidSizeChromosome source) : base(source)
            {
                LimitGeometry = source.LimitGeometry;
            }

            public Geometry LimitGeometry { get; }

            public double Size
            {
                get
                {
                    return (Value[1] - Value[0]) * (Value[3] - Value[2]);
                }
            }

            public override IChromosome CreateNew()
            {
                return new ValidSizeChromosome(LimitGeometry);
            }

            public override IChromosome Clone()
            {
                return new ValidSizeChromosome(this);
            }

            public override void Mutate()
            {
                int maxIteration = 100;
                switch (Generator.Random.Next(length))
                {
                    case 0:
                        for (int i = 0; i < maxIteration; i++)
                        {
                            Value[0] = (ushort)Generator.Random.Next((int)Math.Ceiling(LimitGeometry.Bounds.Left), (int)Math.Floor(LimitGeometry.Bounds.Left + LimitGeometry.Bounds.Width / 2));
                            if (LimitGeometry.FillContains(new System.Windows.Point(Value[0], Value[2]))
                                && LimitGeometry.FillContains(new System.Windows.Point(Value[0], Value[3]))) break;
                        }
                        break;
                    case 1:
                        for (int i = 0; i < maxIteration; i++)
                        {
                            Value[1] = (ushort)Generator.Random.Next((int)Math.Ceiling(LimitGeometry.Bounds.Left + LimitGeometry.Bounds.Width / 2), (int)Math.Floor(LimitGeometry.Bounds.Right));
                            if (LimitGeometry.FillContains(new System.Windows.Point(Value[1], Value[2]))
                                && LimitGeometry.FillContains(new System.Windows.Point(Value[1], Value[3]))) break;
                        }
                        break;
                    case 2:
                        for (int i = 0; i < maxIteration; i++)
                        {
                            Value[2] = (ushort)Generator.Random.Next((int)Math.Ceiling(LimitGeometry.Bounds.Top), (int)Math.Floor(LimitGeometry.Bounds.Top + LimitGeometry.Bounds.Height / 2));
                            if (LimitGeometry.FillContains(new System.Windows.Point(Value[0], Value[2]))
                                && LimitGeometry.FillContains(new System.Windows.Point(Value[1], Value[2]))) break;
                        }
                        break;
                    case 3:
                        for (int i = 0; i < maxIteration; i++)
                        {
                            Value[3] = (ushort)Generator.Random.Next((int)Math.Ceiling(LimitGeometry.Bounds.Top + LimitGeometry.Bounds.Height / 2), (int)Math.Floor(LimitGeometry.Bounds.Bottom));
                            if (LimitGeometry.FillContains(new System.Windows.Point(Value[0], Value[3]))
                                && LimitGeometry.FillContains(new System.Windows.Point(Value[1], Value[3]))) break;
                        }
                        break;
                }
            }

            public override void Generate()
            {
                if (LimitGeometry != null)
                {
                    Value[0] = (ushort)Generator.Random.Next((int)Math.Ceiling(LimitGeometry.Bounds.Left), (int)Math.Floor(LimitGeometry.Bounds.Left + LimitGeometry.Bounds.Width / 2));
                    Value[1] = (ushort)Generator.Random.Next((int)Math.Ceiling(LimitGeometry.Bounds.Left + LimitGeometry.Bounds.Width / 2), (int)Math.Floor(LimitGeometry.Bounds.Right));
                    Value[2] = (ushort)Generator.Random.Next((int)Math.Ceiling(LimitGeometry.Bounds.Top), (int)Math.Floor(LimitGeometry.Bounds.Top + LimitGeometry.Bounds.Height / 2));
                    Value[3] = (ushort)Generator.Random.Next((int)Math.Ceiling(LimitGeometry.Bounds.Top + LimitGeometry.Bounds.Height / 2), (int)Math.Floor(LimitGeometry.Bounds.Bottom));
                }
            }
        }

        class ValidSizeFitnessFunction : IFitnessFunction
        {
            public double Evaluate(IChromosome chromosome)
            {
                if (chromosome is ValidSizeChromosome validSizeChromosome)
                {
                    if (!validSizeChromosome.LimitGeometry.FillContains(new System.Windows.Point(validSizeChromosome.Value[0], validSizeChromosome.Value[2]))
                        || !validSizeChromosome.LimitGeometry.FillContains(new System.Windows.Point(validSizeChromosome.Value[0], validSizeChromosome.Value[3]))
                        || !validSizeChromosome.LimitGeometry.FillContains(new System.Windows.Point(validSizeChromosome.Value[1], validSizeChromosome.Value[2]))
                        || !validSizeChromosome.LimitGeometry.FillContains(new System.Windows.Point(validSizeChromosome.Value[1], validSizeChromosome.Value[3])))
                        return 0;

                    return validSizeChromosome.Size;
                }
                return 0;
            }
        }
    }
}
