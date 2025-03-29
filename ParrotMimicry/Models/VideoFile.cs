using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ParrotMimicry.Models
{
    public class VideoFile : INotifyPropertyChanged
    {
        private string _filePath;
        private string _fileName;
        private bool _isConverting;
        private double _progress;

        public string FilePath
        {
            get => _filePath;
            set
            {
                if (_filePath != value)
                {
                    _filePath = value;
                    OnPropertyChanged();
                }
            }
        }

        public string FileName
        {
            get => _fileName;
            set
            {
                if (_fileName != value)
                {
                    _fileName = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsConverting
        {
            get => _isConverting;
            set
            {
                if (_isConverting != value)
                {
                    _isConverting = value;
                    OnPropertyChanged();
                }
            }
        }

        public double Progress
        {
            get => _progress;
            set
            {
                if (_progress != value)
                {
                    _progress = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}