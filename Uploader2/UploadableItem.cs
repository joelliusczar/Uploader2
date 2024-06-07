using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Uploader2
{
    public class UploadableItem: INotifyPropertyChanged
    {
        [Browsable(false)]
        public string Path { get; set; }
        
        [Browsable(false)]
        public string RootPath { get; set; }

        [Browsable(false)]
        public long SizeMB { get; set; }
        public string AWSKey => Path?.Replace(RootPath ?? "", "")?.Trim('\\')?.Replace('\\','/');

        private int _percentDone;

        public int PercentDone
        {
            get { return _percentDone; }
            set 
            { 
                _percentDone = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PercentDone)));
            }
        }

        private string _status;

        public string Status
        {
            get { return _status; }
            set 
            { 
                _status = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
            }
        }

        private string _uploaded;

        public string Uploaded
        {
            get { return _uploaded; }
            set 
            { 
                _uploaded = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Uploaded)));
            }
        }


        public event PropertyChangedEventHandler PropertyChanged;
    }
}
