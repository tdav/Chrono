using Chrono.ViewModels;

namespace Chrono.Views;

public partial class AlarmEditPage : ContentPage
{
    private readonly AlarmEditViewModel viewModel;

    public AlarmEditPage(AlarmEditViewModel viewModel)
    {
        this.InitializeComponent();
        this.viewModel = viewModel;
        this.BindingContext = viewModel;
    }

    protected override void OnDisappearing()
    {
        // Прослушивание мелодии не должно продолжаться после ухода со страницы (в т.ч. системной кнопкой «Назад»).
        this.viewModel.StopPreview();
        base.OnDisappearing();
    }

    private void OnDigitsChanged(object? sender, TextChangedEventArgs e)
    {
        var entry = (Entry)sender!;
        var text = e.NewTextValue ?? "";

        // Цифровая клавиатура Android пропускает «.», «,», «-» и пробел — вычищаем их из самого поля.
        var digits = new string(text.Where(char.IsAsciiDigit).ToArray());
        if (digits != text)
        {
            entry.Text = digits;

            // После программной замены текста Android оставляет курсор на старом индексе; новый текст
            // доезжает до нативного поля позже события, поэтому установка отложенная.
            this.Dispatcher.Dispatch(() => entry.CursorPosition = entry.Text?.Length ?? 0);
            return;
        }

        // Поле заполнено набором (а не загрузкой значения) — фокус переходит к следующему.
        if (entry.IsFocused && digits.Length == entry.MaxLength && (e.OldTextValue?.Length ?? 0) < digits.Length)
        {
            // Для ежедневного и по дням недели строки даты нет — после минут ввод закончен.
            var next = entry == this.HourEntry ? this.MinuteEntry
                : entry == this.MinuteEntry && this.viewModel.IsDateVisible ? this.DayEntry
                : entry == this.DayEntry ? this.MonthEntry
                : entry == this.MonthEntry ? this.YearEntry
                : null;

            if (next is null)
            {
                // Unfocus на Android клавиатуру не закрывает — скрываем явно.
                _ = entry.HideSoftInputAsync(CancellationToken.None);
                entry.Unfocus();
            }
            else
            {
                next.Focus();
            }
        }
    }

    private void OnDigitsFocused(object? sender, FocusEventArgs e)
    {
        var entry = (Entry)sender!;

        // Значение выделяется целиком: набор заменяет его, а не упирается в MaxLength.
        this.Dispatcher.Dispatch(() =>
        {
            entry.CursorPosition = 0;
            entry.SelectionLength = entry.Text?.Length ?? 0;
        });
    }
}
