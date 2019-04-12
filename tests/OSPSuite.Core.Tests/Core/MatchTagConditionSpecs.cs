using OSPSuite.BDDHelper;
using OSPSuite.BDDHelper.Extensions;
using OSPSuite.Core.Domain.Descriptors;

namespace OSPSuite.Core
{
   public abstract class concern_for_MatchTagCondition : ContextSpecification<MatchTagCondition>
   {
      protected Tags _tags;
      protected IDescriptorCondition _match;
      protected IDescriptorCondition _doNotMatch;
      protected EntityDescriptor _entityCriteria;

      protected override void Context()
      {
         _tags = new Tags {new Tag("tag1"), new Tag("tag2")};
         _match = new MatchTagCondition("tag1");
         _doNotMatch = new MatchTagCondition("do not match");
         _entityCriteria = new EntityDescriptor {Tags = _tags};
      }
   }

   public class When_checking_if_a_match_condition_matches_a_given_tag_set : concern_for_MatchTagCondition
   {
      [Observation]
      public void check_that_string_representation_is_accurate()
      {
         _match.ToString().ShouldBeEqualTo("tag1");
      }

      [Observation]
      public void should_return_true_if_the_tags_contain_the_matching_element()
      {
         _match.IsSatisfiedBy(_entityCriteria).ShouldBeTrue();
      }

      [Observation]
      public void should_return_false_if_the_tags_do_not_contain_the_matching_element()
      {
         _doNotMatch.IsSatisfiedBy(_entityCriteria).ShouldBeFalse();
      }
   }
}