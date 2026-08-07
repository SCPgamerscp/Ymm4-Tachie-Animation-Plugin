using System.Windows;
using System.Windows.Data;
using YukkuriMovieMaker.Commons;
using Ymm4TachieAnimationPlugin.Editor.Controls;

namespace Ymm4TachieAnimationPlugin.Plugin.Parameters;

public class DirectorySelectorWithRigEditorAttribute : PropertyEditorAttribute2
{
    public override FrameworkElement Create()
    {
        return new DirectorySelectorWithRigEditor();
    }

    public override void SetBindings(FrameworkElement control, ItemProperty[] properties)
    {
        if (control is DirectorySelectorWithRigEditor selector && properties.Length > 0)
        {
            var prop = properties[0];
            var binding = new Binding("Value")
            {
                Source = prop,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
            };
            selector.SetBinding(DirectorySelectorWithRigEditor.ValueProperty, binding);
        }
    }

    public override void ClearBindings(FrameworkElement control)
    {
        if (control is DirectorySelectorWithRigEditor selector)
        {
            BindingOperations.ClearBinding(selector, DirectorySelectorWithRigEditor.ValueProperty);
        }
    }
}
