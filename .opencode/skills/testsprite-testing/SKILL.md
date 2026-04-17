---
name: testsprite-testing
description: |-
  Use this skill to automate the software testing lifecycle, including codebase analysis, PRD generation, test planning, and end-to-end test execution.
  It should be used after code generation or modification to verify functionality and ensure high quality.
  Example usage:
  - User says "Test the new rotation feature".
  - Proactively use it to verify a complex PR before delivery.
  - Use it to generate a backend or frontend test plan for a new application.
---

# TestSprite Autonomous Testing Skill

This skill leverages TestSprite to automate the verification of code changes and the generation of comprehensive test suites.

## Workflow

1. **Bootstrap (if needed):** Before any other operation, check if `.testsprite/config.json` exists. If not, call `TestSprite_testsprite_bootstrap`.
2. **Analyze Codebase:** Use `TestSprite_testsprite_generate_code_summary` to get a high-level understanding of the project structure and logic.
3. **Generate PRD:** Use `TestSprite_testsprite_generate_standardized_prd` to create a structured Product Requirements Document based on the current implementation.
4. **Create Test Plan:** 
   - For UI/Frontend: Call `TestSprite_testsprite_generate_frontend_test_plan`.
   - For API/Backend: Call `TestSprite_testsprite_generate_backend_test_plan`.
5. **Execute Tests:** Once the plan is ready and the local service is running, call `TestSprite_testsprite_generate_code_and_execute` to generate the test code and run it.
6. **Review Results:** Analyze the markdown report returned. If tests fail, use `TestSprite_testsprite_open_test_result_dashboard` to debug and modify steps via the web interface.

## Verification Standards
- Always ensure the project is buildable before running execution tools.
- Favor production mode (`npm run build && npm run start`) for more accurate results, though dev mode is supported with limitations.
- Check account info with `TestSprite_testsprite_check_account_info` if quota issues arise.
