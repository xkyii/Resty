using Aprillz.MewUI;
using Aprillz.MewUI.Controls;

var window = new Window()
    .Title("Resty — G0 Spike")
    .Resizable(520, 360)
    .Padding(16)
    .Content(
        new StackPanel()
            .Spacing(12)
            .Children(
                new Label().Text("Resty GUI — MewUI + NativeAOT Spike").FontSize(18).Bold(),
                new Label().Text("If you can read this, the AOT spike succeeded.").FontSize(13),
                new Button().Content("Quit").OnClick(() => Application.Quit())
            )
    );

Application.Run(window);
