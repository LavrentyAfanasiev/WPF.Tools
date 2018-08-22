﻿using System.Collections.Generic;
using System.Linq;
using Ui.Wpf.KanbanControl.Dimensions;
using Ui.Wpf.KanbanControl.Elements.CardElement;
using Ui.Wpf.KanbanControl.Expressions;

namespace Ui.Wpf.KanbanControl.ElementsManagement
{
    internal class ElementsDispenser : IElementsDispenser
    {
        public void DispenceItems(
            ICollection<Card> cards, 
            IDimension horizontalDimention,
            IDimension verticalDimension,
            PropertyAccessorsExpressionCreator propertyAccessors)
        {
            if (cards.Count == 0)
                return;
            
            
            foreach (var card in cards)
            {
                if (horizontalDimention is IDimensionIndexSource horizontalDimentionWithIndex)
                {
                    card.HorizontalCategoryIndex = horizontalDimentionWithIndex.GetDimensionIndex(card.Item);
                }
                else
                {
                    card.HorizontalCategoryIndex = GetDimensionIndex(
                        horizontalDimention, 
                        card.Item, 
                        propertyAccessors);
                }

                if (verticalDimension is IDimensionIndexSource verticalDimensionWithIndex)
                {
                    card.VerticalCategoryIndex = verticalDimensionWithIndex.GetDimensionIndex(card.Item);                    
                }
                else
                {
                    card.VerticalCategoryIndex = GetDimensionIndex(
                        verticalDimension, 
                        card.Item,
                        propertyAccessors);
                }
            }
        }
        
        public int GetDimensionIndex(
            IDimension dimension, 
            object item,
            PropertyAccessorsExpressionCreator propertyAccessors)
        {
            var getter = propertyAccessors.TakeGetterForProperty(dimension.ExpressionPath);
            var itemTag = getter(item);

            var categoryIndex = 0;
            // all Categories must be TagsDimensionCategory
            foreach (var category in dimension.Categories.OfType<TagsDimensionCategory>())
            {
                if (category.Tags.Contains(itemTag))
                {
                    return categoryIndex;
                }

                categoryIndex++;
            }

            return -1;
        }
    }
}
