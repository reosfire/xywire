using CommunityToolkit.Mvvm.Input;
using UI.Models;

namespace UI.PageModels;

public interface IProjectTaskPageModel
{
    IAsyncRelayCommand<ProjectTask> NavigateToTaskCommand { get; }
    bool IsBusy { get; }
}