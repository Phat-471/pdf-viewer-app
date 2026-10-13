using System;
using System.Threading;
using System.Threading.Tasks;

namespace PdfViewerApp;

internal static class PrintWorker
{
	// PrintQueue, PrintTicket and WPF visuals belong to the STA that creates them.
	public static Task RunAsync(Action action)
	{
		var completion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
		var thread = new Thread(() =>
		{
			try { action(); completion.SetResult(null); }
			catch (Exception ex) { completion.SetException(ex); }
		}) { IsBackground = true, Name = "PDF print worker" };
		thread.SetApartmentState(ApartmentState.STA);
		thread.Start();
		return completion.Task;
	}
}
