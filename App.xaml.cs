using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace WarpGenerator
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Глобальные обработчики необработанных исключений: показать понятное
            // сообщение вместо тихого краша приложения. Регистрируем до base.OnStartup,
            // чтобы поймать в том числе ошибки при создании главного окна.
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            base.OnStartup(e);
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // Исключение в UI-потоке: показываем сообщение и продолжаем работу.
            ShowError("Произошла непредвиденная ошибка в интерфейсе.", e.Exception);
            e.Handled = true;
        }

        private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            // Исключение вне UI-потока: как правило, приводит к завершению процесса.
            ShowError(
                e.IsTerminating
                    ? "Произошла критическая ошибка, приложение будет закрыто."
                    : "Произошла непредвиденная фоновая ошибка.",
                e.ExceptionObject as Exception);
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            // Необработанное исключение в асинхронной задаче: помечаем как обработанное,
            // чтобы не завершать процесс, и уведомляем пользователя.
            ShowError("Произошла непредвиденная ошибка в фоновой задаче.", e.Exception);
            e.SetObserved();
        }

        private static void ShowError(string header, Exception? ex)
        {
            string details = string.IsNullOrWhiteSpace(ex?.Message) ? "Подробности недоступны." : ex!.Message;
            string message = $"{header}\n\n{details}";

            // MessageBox может вызываться из не-UI-потока — маршалим в поток диспетчера.
            Dispatcher? dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(() => ShowBox(message));
            }
            else
            {
                ShowBox(message);
            }
        }

        private static void ShowBox(string message)
        {
            MessageBox.Show(message, "WARP Генератор — ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
