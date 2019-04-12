﻿using System;

namespace OSPSuite.Core.Domain.Descriptors
{
   public class NotMatchTagCondition : ITagCondition
   {
      public string Tag { get; private set; }

      public string Condition => $"{Constants.NOT.ToUpper()} {Tag}";

      [Obsolete("For serialization")]
      public NotMatchTagCondition()
      {
      }

      public override string ToString() => Condition;

      public NotMatchTagCondition(string tag)
      {
         Tag = tag;
      }

      public bool IsSatisfiedBy(EntityDescriptor entityDescriptor)
      {
         return !entityDescriptor.Tags.Contains(Tag);
      }

      public IDescriptorCondition CloneCondition()
      {
         return new NotMatchTagCondition(Tag);
      }

      public void Replace(string keyword, string replacement)
      {
         if (string.Equals(Tag, keyword))
            Tag = replacement;
      }

      protected bool Equals(NotMatchTagCondition other)
      {
         return string.Equals(Tag, other.Tag);
      }

      public override bool Equals(object obj)
      {
         if (ReferenceEquals(null, obj)) return false;
         if (ReferenceEquals(this, obj)) return true;
         if (obj.GetType() != this.GetType()) return false;
         return Equals((NotMatchTagCondition) obj);
      }

      public override int GetHashCode() => Condition.GetHashCode();
   }
}