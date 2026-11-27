# Professional Decision-Making

Technical assessment: Redarbor — Senior Developer.

## 1. Describe a recent technical decision you made with incomplete information. What information was missing, and how did you decide to move forward?

In my previous job, I maintained a legacy insurance and reinsurance system. On one occasion, users reported that some brokerage amounts were incorrect.

The challenge was that even the user was not entirely sure how certain calculations were performed. Since it was a legacy system, identifying where all the business logic was implemented was not straightforward either.

Before modifying the code, I decided to review the entire data flow, from the user interface and controllers to the SQL Server stored procedures. I compared the existing data with the expected results and ruled out possible causes until I identified the source of the problem.

Once I identified the cause, I decided to separate the correction of existing records from the changes to the calculation logic for future records, avoiding any impact on financial information that was already correct.

In situations like this, I prefer to move forward with controlled changes supported by data and tests rather than make assumptions about a business rule I do not yet fully understand.

## 2. When two technical approaches seem equally valid, what criteria do you use to choose one?

The first thing I do is understand the problem we are trying to solve and the actual needs of the business.

If both approaches meet the requirements, I consider factors such as maintainability, performance, complexity, security, implementation effort, and their impact on the existing system and the development team.

I also consider how easy it will be to test, maintain, and extend the solution in the future. If there are still doubts, I prefer to build a small proof of concept that allows me to compare both approaches using actual results.

I would not necessarily choose the newest technology or the most complex architecture. I prefer a solution that meets current business needs and can evolve without introducing unnecessary complexity.

## 3. Describe a situation in which you had to reverse or change one of your own technical decisions. What did you learn from that experience?

One of the most challenging projects I worked on was developing a module to visually represent the distribution of risks across different coverage layers in an insurance and reinsurance system.

The challenge was not just implementing the business logic on the backend. It also involved designing and developing an interactive user interface within a legacy system that did not have this type of functionality. I had to find a way to present complex information visually and dynamically, making it easy for users to understand while respecting the technical limitations of the existing system.

Initially, I developed an algorithm that positioned risk participations based on adjacent coverage layers. Although it worked correctly in several scenarios, testing more complex distributions revealed cases where the results were inconsistent.

I decided to replace the algorithm with an approach that evaluated the entire distribution and applied validation rules by bands. This required changes to both the processing logic and the visual representation on the frontend.

I also considered that users needed to be able to view and export historical information, even when there were inconsistencies in its distribution, so that the new validation rules would not restrict access to older records.

I learned that a solution can work correctly in certain scenarios without actually solving the entire problem. This experience also reinforced the importance of considering business rules, edge cases, and user experience from the beginning.

I have no problem changing one of my own technical decisions when sound arguments and actual results demonstrate that another approach is better.
