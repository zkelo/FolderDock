using System.Drawing;

namespace FolderDock.Core;

public readonly record struct TaskbarLayout(TaskbarEdge Edge, Rectangle WorkArea);
