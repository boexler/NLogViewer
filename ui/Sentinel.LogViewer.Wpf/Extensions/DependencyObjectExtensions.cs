using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Sentinel.LogViewer.Wpf.Extensions
{
    public static class DependencyObjectExtensions
    {
        /// <summary>
        /// Analyzes both visual and logical tree in order to find an element of a given
        /// type with the specified UID that is a descendant of the <paramref name="source"/> item.
        /// This method ensures that controls in tabs or other containers are found even when not visually active.
        /// </summary>
        /// <typeparam name="T">The type of the queried items.</typeparam>
        /// <param name="source">The root element that marks the source of the search.</param>
        /// <param name="uid">The UID of the <see cref="UIElement"/></param>
        /// <returns>The descendant of <paramref name="source"/> that matches the requested type and UID.</returns>
        public static T? FindChildByUid<T>(this DependencyObject source, string uid) where T : UIElement
        {
            if (source == null)
                return null;
                
            var visited = new HashSet<DependencyObject>(); // Prevent infinite loops
            return FindChildByUidRecursive<T>(source, uid, visited);
        }
        
        private static T? FindChildByUidRecursive<T>(DependencyObject source, string uid, HashSet<DependencyObject> visited) where T : UIElement
        {
            if (source == null || visited.Contains(source))
                return null;
                
            visited.Add(source);
            
            // Check if the source itself matches the criteria
            if (source is T dependencyObject && dependencyObject.Uid.Equals(uid))
            {
                return dependencyObject;
            }
            
            // Search in Visual Tree
            if (source is Visual || source is Visual3D)
            {
                int visualChildrenCount = VisualTreeHelper.GetChildrenCount(source);
                for (int i = 0; i < visualChildrenCount; i++)
                {
                    var visualChild = VisualTreeHelper.GetChild(source, i);
                    if (visualChild != null)
                    {
                        var result = FindChildByUidRecursive<T>(visualChild, uid, visited);
                        if (result != null)
                            return result;
                    }
                }
            }
            
            // Search in Logical Tree
            foreach (object logicalChild in LogicalTreeHelper.GetChildren(source))
            {
                if (logicalChild is DependencyObject logicalDepObj && !visited.Contains(logicalDepObj))
                {
                    var result = FindChildByUidRecursive<T>(logicalDepObj, uid, visited);
                    if (result != null)
                        return result;
                }
            }
            
            return null;
        }
    }
}
