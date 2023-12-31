using System.Linq;
using System.Numerics;
using Content.Client.Stylesheets;
using Content.Shared.Ganimed;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Prototypes;
using Robust.Shared.Network;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.Ganimed.BookTerminal
{
    [GenerateTypedNameReferences]
    public sealed partial class BookTerminalWindow : DefaultWindow
    {
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
		[Dependency] private readonly IEntityManager _entMan = default!;
		public event Action<BaseButton.ButtonEventArgs, PrintBookButton>? OnPrintBookButtonPressed;
        public event Action<GUIMouseHoverEventArgs, PrintBookButton>? OnPrintBookButtonMouseEntered;
        public event Action<GUIMouseHoverEventArgs, PrintBookButton>? OnPrintBookButtonMouseExited;


        public BookTerminalWindow()
        {
            RobustXamlLoader.Load(this);
            IoCManager.InjectDependencies(this);
        }

        public void UpdateState(BoundUserInterfaceState state)
        {
            var castState = (BookTerminalBoundUserInterfaceState) state;
            UpdateContainerInfo(castState);
            UpdateBooksList(castState);
			
			CopyPasteButton.Text = Loc.GetString(castState.CopyPaste 
									? "book-terminal-window-paste-button" 
									: "book-terminal-window-copy-button");
			
            UploadButton.Disabled = !castState.RoutineAllowed || castState.WorkProgress is not null;
			ClearButton.Disabled = !castState.RoutineAllowed || castState.WorkProgress is not null;
            EjectButton.Disabled = castState.BookName is null || castState.WorkProgress is not null;
			CopyPasteButton.Disabled = !castState.RoutineAllowed || castState.WorkProgress is not null;
        }
		
		public void UpdateBooksList(BoundUserInterfaceState state)
        {
            var castState = (BookTerminalBoundUserInterfaceState) state;
			
			if (BooksList == null)
                return;

            BooksList.Children.Clear();
			
			if (castState.BookEntries is null)
				return;
			

            foreach (var entry in castState.BookEntries.OrderBy(r => r.Name))
            {
                var button = new PrintBookButton(entry, CutDescription(entry.Name ?? ""));
                button.OnPressed += args => OnPrintBookButtonPressed?.Invoke(args, button);
                button.OnMouseEntered += args => OnPrintBookButtonMouseEntered?.Invoke(args, button);
                button.OnMouseExited += args => OnPrintBookButtonMouseExited?.Invoke(args, button);
				button.Disabled = !castState.RoutineAllowed || castState.WorkProgress is not null;
                BooksList.AddChild(button);
            }
        }
		
		public string CutDescription(string? text)
		{
			if (text is null)
				return "";
			
			if (text.Length <= 47)
				return text;
			
			return text.Substring(0, 47) + "...";
		}

        public void UpdateContainerInfo(BookTerminalBoundUserInterfaceState state)
        {
            ContainerInfo.Children.Clear();
			
			if (state.WorkProgress is not null)
			{
				ContainerInfo.Children.Add(new Label {Text = Loc.GetString("book-terminal-window-working", ("progress", (int)(100.0f * (1 - state.WorkProgress)))) });
				return;
			}

            if (state.BookName is null)
            {
                ContainerInfo.Children.Add(new Label {Text = Loc.GetString("book-terminal-window-no-book-loaded-text") });
            } else {
			
				var bookPreview = new SpriteView
				{
					Scale = new Vector2(2, 2),
					OverrideDirection = Direction.South,
					VerticalAlignment = VAlignment.Center,
					SizeFlagsStretchRatio = 1
				};
				if (_entMan.TryGetEntity(state.BookEntity, out var bookEntity))
					bookPreview.SetEntity(bookEntity);
				
				var bookLabel = new Label
				{
					Text = $"{CutDescription(state.BookName)}"
				};
				
				var bookSublabel = new Label
				{
					Text = $"{CutDescription(state.BookDescription)}",
					StyleClasses = {StyleNano.StyleClassLabelSecondaryColor}
				};
				
				var boxInfo = new BoxContainer
				{
					Orientation = LayoutOrientation.Vertical,
					Children = {
						new Control { MinSize = new Vector2(0, 10) },
						bookLabel, 
						bookSublabel }
				};

				ContainerInfo.Children.Add(new BoxContainer
				{
					Orientation = LayoutOrientation.Horizontal,
					Children = { bookPreview, boxInfo }
				});
				
			}
			
			if (state.CartridgeCharge is null)
            {
                ContainerInfo.Children.Add(new Label {Text = Loc.GetString("book-terminal-window-no-cartridge-loaded-text"), FontColorOverride = Color.DarkRed});
                return;
            } else if (state.CartridgeCharge <= -10.0f)
            {
                ContainerInfo.Children.Add(new Label {Text = Loc.GetString("book-terminal-window-cartridge-empty"), FontColorOverride = Color.DarkRed});
                return;
            }
			ContainerInfo.Children.Add(new Label {Text = Loc.GetString("book-terminal-window-cartridge-charge", ("charge", (int) (100 * state.CartridgeCharge)))});
        }
    }
	
	public sealed class PrintBookButton : Button {
        public SharedBookTerminalEntry BookEntry { get; }

        public PrintBookButton(SharedBookTerminalEntry bookEntry, string text)
        {
            BookEntry = bookEntry;
            Text = text;
        }
    }
}
