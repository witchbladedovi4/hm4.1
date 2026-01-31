using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace WpfApp3
{
    public partial class MainWindow : Window
    {
        private SemaphoreSlim _fileSemaphore = new SemaphoreSlim(3, 3); 
        private ReaderWriterLockSlim _fileLock = new ReaderWriterLockSlim();
        private string _logFilePath = "application.log";

        private SemaphoreSlim _dbSemaphore = new SemaphoreSlim(4, 4); 
        private ReaderWriterLockSlim _configLock = new ReaderWriterLockSlim();
        private DatabaseConfig _databaseConfig;

        private Mutex _appMutex;
        private const string MUTEX_NAME = @"Global\{FileDbManager-8A3B7C2D-4F9E-41A6-B8C3-D5E7F9A1B2C3}";

        private DispatcherTimer _updateTimer;

        public MainWindow()
        {
            InitializeComponent();

            _databaseConfig = new DatabaseConfig
            {
                ConnectionString = "Server=localhost;Database=TestDB;Trusted_Connection=True;",
                MaxRetryAttempts = 3,
                TimeoutSeconds = 30
            };

            InitializeMutex();

            InitializeLogFile();

            _updateTimer = new DispatcherTimer();
            _updateTimer.Interval = TimeSpan.FromMilliseconds(500);
            _updateTimer.Tick += UpdateTimer_Tick;
            _updateTimer.Start();

            AddLog("Приложение инициализировано");
            AddLog($"Файл лога: {Path.GetFullPath(_logFilePath)}");
        }

        private void InitializeMutex()
        {
            try
            {
                bool createdNew;
                _appMutex = new Mutex(true, MUTEX_NAME, out createdNew);

                if (!createdNew)
                {
                    MessageBox.Show("Приложение уже запущено!", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    Application.Current.Shutdown();
                }
            }
            catch (Exception ex)
            {
                AddLog($"Ошибка инициализации Mutex: {ex.Message}");
            }
        }

        private void InitializeLogFile()
        {
            try
            {
                if (!File.Exists(_logFilePath))
                {
                    File.WriteAllText(_logFilePath, $"Лог приложения запущен: {DateTime.Now}\n");
                }
            }
            catch (Exception ex)
            {
                AddLog($"Ошибка создания файла лога: {ex.Message}");
            }
        }

        #region 1

        private async void ButtonStart_Click(object sender, RoutedEventArgs e)
        {
            buttonStart.IsEnabled = false;
            txtStatus.Text = "Выполняется тест записи файлов...";

            try
            {
                AddLog("=== Начало теста записи файлов (5 задач) ===");

                List<Task> tasks = new List<Task>();

                for (int i = 1; i <= 5; i++)
                {
                    int taskId = i;
                    tasks.Add(Task.Run(async () => await WriteToFileTask(taskId)));
                }

                await Task.WhenAll(tasks);
                AddLog("=== Все 5 задач записи завершены ===");
            }
            catch (Exception ex)
            {
                AddLog($"Ошибка в тесте записи: {ex.Message}");
            }
            finally
            {
                buttonStart.IsEnabled = true;
                txtStatus.Text = "Тест записи завершен";
            }
        }

        private async Task WriteToFileTask(int taskId)
        {
            AddLog($"Задача {taskId} ожидает семафор (макс 3 одновременно)");

            await _fileSemaphore.WaitAsync();

            try
            {
                AddLog($"Задача {taskId} вошла в семафор. Доступно слотов: {_fileSemaphore.CurrentCount}");

                _fileLock.EnterWriteLock();
                try
                {
                    await Task.Delay(1000); 

                    string logMessage = $"[{DateTime.Now:HH:mm:ss}] Задача {taskId} записала данные\n";

                    await File.AppendAllTextAsync(_logFilePath, logMessage);

                    AddLog($"Задача {taskId} записала данные в файл");
                }
                finally
                {
                    _fileLock.ExitWriteLock();
                }

                await Task.Delay(2000); 
                AddLog($"Задача {taskId} завершена");
            }
            finally
            {
                _fileSemaphore.Release();
                AddLog($"Задача {taskId} освободила семафор");
            }
        }

        private async void ButtonClear_Click(object sender, RoutedEventArgs e)
        {
            buttonClear.IsEnabled = false;

            try
            {
                if (_fileLock.TryEnterWriteLock(TimeSpan.FromSeconds(3)))
                {
                    try
                    {
                        AddLog("=== Начало очистки ===");

                        listBox.Items.Clear();

                        await File.WriteAllTextAsync(_logFilePath,
                            $"Лог очищен: {DateTime.Now}\n");

                        AddLog("Логи очищены (UI + файл)");
                        AddLog("=== Очистка завершена ===");
                    }
                    finally
                    {
                        _fileLock.ExitWriteLock();
                    }
                }
                else
                {
                    AddLog("Не удалось получить блокировку для очистки");
                }
            }
            catch (Exception ex)
            {
                AddLog($"Ошибка при очистке: {ex.Message}");
            }
            finally
            {
                buttonClear.IsEnabled = true;
            }
        }

        #endregion

        #region 2

        private async void ButtonDbTest_Click(object sender, RoutedEventArgs e)
        {
            buttonDbTest.IsEnabled = false;
            txtStatus.Text = "Выполняется тест подключений к БД...";

            try
            {
                AddLog("=== Тест подключений к БД (6 задач, макс 4 одновременно) ===");

                var tasks = new List<Task>();
                for (int i = 1; i <= 6; i++)
                {
                    int taskId = i;
                    tasks.Add(Task.Run(async () => await DatabaseConnectionTask(taskId)));
                }

                await Task.WhenAll(tasks);
                AddLog("=== Все подключения к БД завершены ===");
            }
            catch (Exception ex)
            {
                AddLog($"Ошибка в тесте БД: {ex.Message}");
            }
            finally
            {
                buttonDbTest.IsEnabled = true;
                txtStatus.Text = "Тест БД завершен";
            }
        }

        private async Task DatabaseConnectionTask(int taskId)
        {
            AddLog($"Подключение {taskId} ожидает семафор БД");

            await _dbSemaphore.WaitAsync();

            try
            {
                AddLog($"Подключение {taskId} установлено. Активно подключений: {4 - _dbSemaphore.CurrentCount}");

                DatabaseConfig config;
                _configLock.EnterReadLock();
                try
                {
                    config = _databaseConfig;
                    AddLog($"Подкл. {taskId}: Config timeout={config.TimeoutSeconds}s, retries={config.MaxRetryAttempts}");
                }
                finally
                {
                    _configLock.ExitReadLock();
                }

                await Task.Delay(2000);

                if (taskId % 2 == 0)
                {
                    await UpdateDatabaseConfig(taskId);
                }

                AddLog($"Подключение {taskId} завершило работу с БД");
            }
            finally
            {
                _dbSemaphore.Release();
                AddLog($"Подключение {taskId} закрыто");
            }
        }

        private async Task UpdateDatabaseConfig(int taskId)
        {
            _configLock.EnterWriteLock();
            try
            {
                AddLog($"Подкл. {taskId}: Начало обновления конфигурации...");

                await Task.Delay(500); 

                _databaseConfig.TimeoutSeconds += 1;
                _databaseConfig.MaxRetryAttempts = (_databaseConfig.MaxRetryAttempts % 5) + 1;

                AddLog($"Подкл. {taskId}: Конфигурация обновлена. " +
                      $"Новый timeout={_databaseConfig.TimeoutSeconds}s, " +
                      $"retries={_databaseConfig.MaxRetryAttempts}");
            }
            finally
            {
                _configLock.ExitWriteLock();
            }
        }

        private async void ButtonConfig_Click(object sender, RoutedEventArgs e)
        {
            buttonConfig.IsEnabled = false;

            try
            {
                AddLog("=== Тест чтения/записи конфигурации ===");

                var readTasks = new List<Task>();
                for (int i = 1; i <= 3; i++)
                {
                    int readerId = i;
                    readTasks.Add(Task.Run(async () => await ReadConfigTask(readerId)));
                }

                var writeTask = Task.Run(async () => await WriteConfigTask());

                await Task.WhenAll(readTasks);
                await writeTask;

                AddLog("=== Тест конфигурации завершен ===");
            }
            catch (Exception ex)
            {
                AddLog($"Ошибка в тесте конфигурации: {ex.Message}");
            }
            finally
            {
                buttonConfig.IsEnabled = true;
            }
        }

        private async Task ReadConfigTask(int readerId)
        {
            for (int i = 0; i < 3; i++)
            {
                _configLock.EnterReadLock();
                try
                {
                    await Task.Delay(100);
                    AddLog($"Читатель {readerId}.{i}: timeout={_databaseConfig.TimeoutSeconds}s");
                }
                finally
                {
                    _configLock.ExitReadLock();
                }
                await Task.Delay(200);
            }
        }

        private async Task WriteConfigTask()
        {
            _configLock.EnterWriteLock();
            try
            {
                AddLog("Писатель: начало изменения конфигурации...");
                await Task.Delay(300);
                _databaseConfig.TimeoutSeconds += 5;
                AddLog($"Писатель: timeout увеличен до {_databaseConfig.TimeoutSeconds}s");
            }
            finally
            {
                _configLock.ExitWriteLock();
            }
        }

        #endregion

        #region Вспомогательные методы

        private void AddLog(string message)
        {
            Dispatcher.Invoke(() =>
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                listBox.Items.Add($"{timestamp} - {message}");

                if (listBox.Items.Count > 0)
                {
                    listBox.ScrollIntoView(listBox.Items[listBox.Items.Count - 1]);
                }
            });
        }

        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            txtFileSemaphore.Text = $"Файловые операции: {3 - _fileSemaphore.CurrentCount}/3";
            txtDbSemaphore.Text = $"Подключения к БД: {4 - _dbSemaphore.CurrentCount}/4";
            txtConfigVersion.Text = $"Версия конфигурации (timeout): {_databaseConfig.TimeoutSeconds}";
            txtReadLocks.Text = $"Активных чтений: {_configLock.CurrentReadCount}";
            txtWriteLocks.Text = $"Активных записей: {_configLock.WaitingWriteCount}";
        }

        protected override void OnClosed(EventArgs e)
        {
            _updateTimer?.Stop();

            if (_appMutex != null)
            {
                _appMutex.ReleaseMutex();
                _appMutex.Close();
            }

            _fileLock?.Dispose();
            _configLock?.Dispose();

            base.OnClosed(e);
        }

        #endregion
    }
}