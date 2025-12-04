using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WpfApp3
{
    //Представь туалет в клубе:
    //Всего 3 кабинки(максимум 3 человека одновременно)
    //Очередь из людей(потоки)
    //Освободилась кабинка → следующий заходит
    public partial class MainWindow : Window
        
    {
        private SemaphoreSlim writeSemaphone = new SemaphoreSlim(3, 3);
        private ReaderWriterLockSlim fileLock = new ReaderWriterLockSlim();
        public MainWindow()
        {
            InitializeComponent();
            
        }

        private async void ButtonStart_Click(object sender, RoutedEventArgs e)
        {
            buttonStart.IsEnabled = false;
            List<Task> tasks = new List<Task>();

            for (int i = 1; i <= 5; i++)
            {
                int taskId = i;
                tasks.Add(Task.Run(() => WriteTask(taskId)));
                await Task.Delay(100);
            }

            await Task.WhenAll(tasks);
            AddLog("Все 5 задач завершены");
            buttonStart.IsEnabled = true;
        }

        private async void ButtonClear_Click(object sender, RoutedEventArgs e)
        {
            //если написать fire на руском получиться ашку

            fileLock.EnterWriteLock();
            try
            {
                AddLog("- - - Очистка - - - ");
                listBox.Items.Clear();
                AddLog("очишенно");
            }
            finally
            {
                fileLock.ExitWriteLock();
            }
        }
        private async Task WriteTask (int id)
        {
            AddLog($"Задача {id} ждёт симафор");
           
            await writeSemaphone.WaitAsync();
            await Task.Delay(500);
            try
            {
                AddLog($"Задача {id} вошла в симафор ");
                await Task.Delay(5000);

                AddLog($"Задача {id} завершена");
            }
            
            finally
            {
                writeSemaphone.Release();
                AddLog($"Задача {id} освободила симафор");
            }
        }

        private void AddLog(string message)
        {
            Dispatcher.Invoke(() =>
            {
                listBox.Items.Add($"{DateTime.Now:T} - {message}");
            });
        }


    }
}