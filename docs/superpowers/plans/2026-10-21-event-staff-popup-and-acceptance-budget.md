# Event staffing popup and acceptance budget

Goal: Add staff through a popup and calculate the event's payable budget only when HR/Admin accepts the event.

1. Add a persisted flag for new events using acceptance-time compensation. Keep existing events on their recorded budget and allowance flow. Add a migration with a false default.
2. For flagged events, show no budget field on create/edit, leave assignments unpaid, collect only each worker's result during review, then calculate and freeze each completed worker's configured event bonus and the total budget in the acceptance transaction. Make payroll use the frozen amounts.
3. Move the department employee chooser into the existing assignment modal. Keep search, employee filtering, role selection, form validation, and return focus working.
4. Make the create form and popup white. Run policy/integration/UI checks and build the web project.
