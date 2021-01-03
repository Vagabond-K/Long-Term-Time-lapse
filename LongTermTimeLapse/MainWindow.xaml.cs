using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using VagabondK;

namespace LongTermTimeLapse
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : ThemeWindow
    {
        public MainWindow()
        {
            InitializeComponent();

            mainViewModel.TimeLapse.PropertyChanged += TimeLapse_PropertyChanged;
            DataContext = mainViewModel;
        }

        private readonly MainViewModel mainViewModel = new MainViewModel();

        private void TimeLapse_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(mainViewModel.TimeLapse.CurrentFrame):
                    try
                    {
                        Dispatcher.Invoke(() =>
                        {
                            var index = mainViewModel.TimeLapse.Frames.IndexOf(mainViewModel.TimeLapse.CurrentFrame) + 1;
                            if (index < mainViewModel.TimeLapse.Frames.Count)
                                Thumnails.ScrollIntoView(mainViewModel.TimeLapse.Frames[index]);
                            else
                                Thumnails.ScrollIntoView(mainViewModel.TimeLapse.CurrentFrame);
                        });
                    }
                    catch { }
                    break;
            }
        }

    }

}
