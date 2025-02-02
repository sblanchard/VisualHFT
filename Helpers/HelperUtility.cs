﻿using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace VisualHFT;

public class HelperUtility
{
    public static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj)
        where T : DependencyObject
    {
        if (depObj != null)
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                if (child != null && child is T) yield return (T)child;

                foreach (var childOfChild in FindVisualChildren<T>(child)) yield return childOfChild;
            }
    }

    public static childItem FindVisualChild<childItem>(DependencyObject obj)
        where childItem : DependencyObject
    {
        foreach (var child in FindVisualChildren<childItem>(obj)) return child;

        return null;
    }


    /// <summary>
    ///     Finds a Child of a given item in the visual tree.
    /// </summary>
    /// <param name="parent">A direct parent of the queried item.</param>
    /// <typeparam name="T">The type of the queried item.</typeparam>
    /// <param name="childName">x:Name or Name of child. </param>
    /// <returns>
    ///     The first parent item that matches the submitted type parameter.
    ///     If not matching item can be found,
    ///     a null parent is being returned.
    /// </returns>
    public static T FindChild<T>(DependencyObject parent, string childName)
        where T : DependencyObject
    {
        // Confirm parent and childName are valid. 
        if (parent == null) return null;

        T foundChild = null;

        var childrenCount = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < childrenCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            // If the child is not of the request child type child
            var childType = child as T;
            if (childType == null)
            {
                // recursively drill down the tree
                foundChild = FindChild<T>(child, childName);

                // If the child is found, break so we do not overwrite the found child. 
                if (foundChild != null) break;
            }
            else if (!string.IsNullOrEmpty(childName))
            {
                var frameworkElement = child as FrameworkElement;
                // If the child's name is set for search
                if (frameworkElement != null && frameworkElement.Name == childName)
                {
                    // if the child's name is of the request name
                    foundChild = (T)child;
                    break;
                }
            }
            else
            {
                // child element found.
                foundChild = (T)child;
                break;
            }
        }

        return foundChild;
    }
}