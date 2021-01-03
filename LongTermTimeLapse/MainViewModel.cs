using LongTermTimeLapse.VideoExporters;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using System.Windows.Input;

namespace LongTermTimeLapse
{
    public class MainViewModel : NotifyPropertyChangeObject
    {
        public MainViewModel()
        {
            timer = new Timer
            {
                Interval = 100
            };

            timer.Elapsed += Timer_Elapsed;
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (TimeLapse.Frames.Count > 1)
            {
                var index = TimeLapse.Frames.IndexOf(TimeLapse.CurrentFrame);
                if (index < TimeLapse.Frames.Count - 1) index++;
                else index = 0;

                TimeLapse.CurrentFrame = TimeLapse.Frames[index];
            }
        }

        private readonly Timer timer;

        public TimeLapse TimeLapse { get; } = new TimeLapse()
        {
            TimeLapseFrameProcessor = new TimeLapseFrameProcessors.TimeLapseProcessorByFREAK()
        };

        public IVideoExporter VideoExporter { get => Get<IVideoExporter>(); set => Set(value); }

        public bool IsPlaying { get => Get(false); private set => Set(value); }
        public bool IsBusy { get => Get(false); private set => Set(value); }

        public ICommand ImportImagesCommand { get => Get(() =>
        {
            timer.Stop();

            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "All Images|*.jpg;*.bmp;*.png"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                var beforeFrameCount = TimeLapse.Frames.Count;

                IsBusy = true;
                List<TimeLapseFrame> addedFrames = new List<TimeLapseFrame>();

                foreach (string fileName in openFileDialog.FileNames)
                {
                    addedFrames.Add(TimeLapse.AddFrame(fileName));
                }

                if (addedFrames.Count > 0)
                {
                    int initCount = 0;
                    addedFrames.AsParallel().ForAll(async frame =>
                    {
                        await frame.Init();
                        if (addedFrames.IndexOf(frame) == 0)
                            TimeLapse.CurrentFrame = addedFrames[0];
                        initCount++;

                        if (initCount == addedFrames.Count)
                        {
                            if (beforeFrameCount <= 1)
                            {
                                ExportVideoCommand.RaiseCanExecuteChanged();
                                PlayCommand.RaiseCanExecuteChanged();
                                PauseCommand.RaiseCanExecuteChanged();
                                AdjustAllCommand.RaiseCanExecuteChanged();
                            }
                        }
                    });
                }
                IsBusy = false;
            }
        }); }

        public IInstantCommand ExportVideoCommand { get => Get(async () =>
        {
            timer.Stop();

            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "MP4|*.mp4"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                IsBusy = true;

                VideoExporter = new VideoExporterMP4();
                await VideoExporter.Export(TimeLapse, saveFileDialog.FileName);

                IsBusy = false;
            }
        }, () => TimeLapse.Frames.Count > 1); }

        public IInstantCommand PlayCommand { get => Get(() => 
        {
            timer.Start();
            IsPlaying = true; 
        }, () => TimeLapse.Frames.Count > 1); }
        public IInstantCommand PauseCommand { get => Get(() => 
        {
            timer.Stop();
            IsPlaying = false; 
        }, () => TimeLapse.Frames.Count > 1); }

        public IInstantCommand AdjustAllCommand { get => Get(async () => 
        {
            timer.Stop();
            IsBusy = true; 
            await TimeLapse.AdjustAll(); 
            IsBusy = false; 
        }, () => TimeLapse.Frames.Count > 1); }
    }
}
