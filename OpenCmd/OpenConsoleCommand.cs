using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Design;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio;
using Process = System.Diagnostics.Process;
using Task = System.Threading.Tasks.Task;

namespace OpenCmd
{
	/// <summary>
	///     Command handler
	/// </summary>
	internal sealed class OpenConsoleCommand
	{
		/// <summary>
		///     Command ID.
		/// </summary>
		public const int CommandId = 0x0100;

		/// <summary>
		///     Command menu group (command set GUID).
		/// </summary>
		public static readonly Guid CommandSet = new Guid("5cf2eda7-5517-4e2c-9c15-815cebcb8ed1");

		/// <summary>
		///     VS Package that provides this command, not null.
		/// </summary>
		private readonly AsyncPackage _package;

		/// <summary>
		///     Initializes a new instance of the <see cref="OpenConsoleCommand" /> class.
		///     Adds our command handlers for menu (commands must exist in the command table file)
		/// </summary>
		/// <param name="package">Owner package, not null.</param>
		/// <param name="commandService">Command service to add command to, not null.</param>
		private OpenConsoleCommand(AsyncPackage package, OleMenuCommandService commandService)
		{
			_package = package ?? throw new ArgumentNullException(nameof(package));
			commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

			var menuCommandID = new CommandID(CommandSet, CommandId);
			var menuItem = new MenuCommand(OpenConsole, menuCommandID);
			commandService.AddCommand(menuItem);
		}

		/// <summary>
		///     Gets the instance of the command.
		/// </summary>
		public static OpenConsoleCommand Instance { get; private set; }

		/// <summary>
		///     Gets the service provider from the owner package.
		/// </summary>
		private IAsyncServiceProvider ServiceProvider => _package;

		/// <summary>
		///     Initializes the singleton instance of the command.
		/// </summary>
		/// <param name="package">Owner package, not null.</param>
		public static async Task InitializeAsync(AsyncPackage package)
		{
			// Switch to the main thread - the call to AddCommand in OpenConsoleCommand's constructor requires
			// the UI thread.
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

			var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
			Instance = new OpenConsoleCommand(package, commandService);
		}

		/// <summary>
		///     This function is the callback used to execute the command when the menu item is clicked.
		///     See the constructor to see how the menu item is associated with this function using
		///     OleMenuCommandService service and MenuCommand class.
		/// </summary>
		/// <param name="sender">Event sender.</param>
		/// <param name="e">Event args.</param>
		private void Execute(object sender, EventArgs e)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var message = string.Format(CultureInfo.CurrentCulture, "Inside {0}.MenuItemCallback()", GetType().FullName);
			var title = "OpenConsoleCommand";

			// Show a message box to prove we were here
			VsShellUtilities.ShowMessageBox(
				_package,
				message,
				title,
				OLEMSGICON.OLEMSGICON_INFO,
				OLEMSGBUTTON.OLEMSGBUTTON_OK,
				OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST
			);
		}

		private void OpenConsole(object sender, EventArgs e)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			var dte = ServiceProvider.GetServiceAsync(typeof(DTE)).Result as DTE;
			if (dte == null) return;

			if (dte.SelectedItems.Count < 1) return;
			var item = dte.SelectedItems.Item(1);
			if (item == null) return;

			var fullName =
				item.ProjectItem?.Properties.OfType<Property>().FirstOrDefault(a => a.Name == "FullPath")?.Value?.ToString()
				?? item.ProjectItem?.Document?.FullName
				?? item.Project.FullName;

			if (string.IsNullOrWhiteSpace(fullName)) return;

			var path = Path.GetDirectoryName(fullName);

			if (string.IsNullOrWhiteSpace(path)) return;

			var proc = new Process();
			proc.StartInfo.FileName = "cmd.exe";
			proc.StartInfo.WorkingDirectory = path;
			proc.Start();
		}
	}
}
