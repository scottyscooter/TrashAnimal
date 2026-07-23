# API Contract Tests

These tests verify that the **frontend TypeScript types and API client code are synchronized with real TrashAnimal.Api responses**. They detect contract mismatches that would cause runtime failures in production.

## Why These Tests Matter

Contract mismatches between frontend and backend typically manifest as:
- Type errors hidden until runtime (JSON deserialization failures)
- Incorrect request format (old vs new polymorphic union structure)
- Enum serialization mismatches (strings vs numeric enums)
- Missing or unexpected fields in responses
- Null/optional field handling bugs

These tests catch all of these **before the code ships**, by making real API calls and validating against the TypeScript definitions in `src/api/types.ts`.

## Files

### 1. **`src/api/contracts.test.ts`** — Vitest (Node.js) Tests
- Fastest (~3-4 seconds)
- Runs without browser overhead
- **Recommended for CI/CD**

**Run:**
```bash
npm run test:run -- contracts.test.ts      # Run once (CI)
npm run test -- contracts.test.ts          # Watch mode (dev)
```

### 2. **`e2e/api-contract.spec.ts`** — Playwright Tests
- Runs in real browsers (Chromium, Firefox, WebKit)
- Validates HTTP-level protocol details
- Slower (~30-40 seconds for all browsers)

**Run:**
```bash
npm run test:e2e e2e/api-contract.spec.ts
```

## What They Test

### Polymorphic Union Structure (PR #18 Breaking Change)
- ✅ Old flat `SubmitCommandRequest` format **rejects** (missing `kind` field)
- ✅ `PlayActionCommand` with `"kind": "action"`
- ✅ `PlayFeeshCommand` with `"kind": "playFeesh"`
- ✅ `PlayShinyCommand` with `"kind": "playShiny"`
- ✅ `ResolveTokenStealCommand` with `"kind": "resolveTokenSteal"`
- ✅ `CardPickCommand` with `"kind": "cardPick"`
- ✅ `DoubleStashCommand` with `"kind": "doubleStash"`
- ✅ `RecyclePickCommand` with `"kind": "recyclePick"`

### Response Shapes
- ✅ `GameCreationResponse` has `gameId`, `view`, `allowedActions`
- ✅ `PlayerViewResponse` has `view`, `allowedActions`, `revision`
- ✅ `GameCommandResponse` has `succeeded`, `errorMessage`, `view`, `allowedActions`
- ✅ `GameResultResponse` has `scoreLines`, `winningPlayerIndex`

### Enum Serialization (String, Not Numeric)
- ✅ `GameState` → `'RollPhase'` (not `0`)
- ✅ `GameAction` → `'RollDie'` (not `1`)
- ✅ `CardName` → `'Blammo'` (not `0`)
- ✅ `TokenAction` → `'StashTrash'` (not `0`)

### Field Naming Convention
- ✅ camelCase response fields (`gameId`, not `GameId`)
- ✅ camelCase nested fields (`currentPlayerIndex`, not `CurrentPlayerIndex`)
- ✅ No PascalCase field names exist

### Nullable/Optional Fields
- ✅ `stealPhase` can be `null` or `StealPhaseView`
- ✅ `tokenPhase` can be `null` or `TokenPhaseView`
- ✅ `yumYumResponderIndex` can be `null` or `number`
- ✅ `yumYumResponderName` can be `null` or `string`

### HTTP Status Codes
- ✅ `POST /games` → `201 Created`
- ✅ `GET /games/{id}/view` → `200 OK`
- ✅ `POST /games/{id}/commands` → `200 OK` (success) or `422 Unprocessable Entity` (rule rejection)
- ✅ Error responses are always structured (never arbitrary JSON)

## Prerequisites: Start the Backend

**Terminal 1:** Start TrashAnimal.Api
```bash
cd TrashAnimal.Api
dotnet run
```

Verify it's running:
```bash
curl http://localhost:5080/openapi/v1.json
```

**Terminal 2:** Run tests
```bash
cd TrashAnimal.Web
npm run test:run -- contracts.test.ts
```

## When Tests Fail

### Example 1: Old Request Format (Before Frontend Update)
```
FAIL: rejects old flat format (must use "kind" discriminator)
  expected 200 to be one of: 400, 422, 500
```
**Meaning:** Frontend is still sending old `SubmitCommandRequest` format.
**Action:** Update `gamesApi.ts` to use polymorphic union with `kind` field.

### Example 2: Missing "kind" Field
```
FAIL: accepts PlayActionCommand with kind: "action"
  POST request failed: 400 Bad Request
```
**Meaning:** Backend rejected the request (likely validation error).
**Action:** Verify request includes `kind` field: `{ kind: 'action', playerSeat: 0, action: 'RollDie' }`

### Example 3: Backend Not Running
```
Error: Failed to create test game: 0 Connection refused.
Is TrashAnimal.Api running at http://localhost:5080?
```
**Action:** Start the backend (see Prerequisites above).

### Example 4: Enum Serialization Mismatch
```
FAIL: GameState is a string (not numeric)
  expected 0 to be a string
```
**Meaning:** Backend returned numeric enum instead of string.
**Action:** Verify `JsonStringEnumConverter` is configured in backend `Program.cs`.

## CI/CD Integration

### GitHub Actions Example
```yaml
# .github/workflows/integration-tests.yml
name: API Contract Tests

on: [push, pull_request]

jobs:
  contract-tests:
    runs-on: ubuntu-latest
    
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '10.0.x'
      
      - name: Setup Node.js
        uses: actions/setup-node@v3
        with:
          node-version: '20'
      
      - name: Start backend
        working-directory: TrashAnimal.Api
        run: dotnet run &
        timeout-minutes: 2
      
      - name: Wait for API
        run: |
          for i in {1..30}; do
            curl -f http://localhost:5080/openapi/v1.json && exit 0
            sleep 1
          done
          exit 1
      
      - name: Install frontend dependencies
        working-directory: TrashAnimal.Web
        run: npm install
      
      - name: Run contract tests
        working-directory: TrashAnimal.Web
        run: npm run test:run -- contracts.test.ts
```

## Debugging Failed Tests

### 1. Test Against Running Backend Manually
```bash
# Create a game
curl -X POST http://localhost:5080/games \
  -H "Content-Type: application/json" \
  -d '{"playerNames": ["Alice", "Bob"]}'

# Submit command with NEW format
curl -X POST http://localhost:5080/games/{gameId}/commands \
  -H "Content-Type: application/json" \
  -d '{
    "kind": "action",
    "playerSeat": 0,
    "action": "RollDie"
  }'

# Compare to types.ts and gamesApi.ts
```

### 2. Enable Debug Output
```bash
npm run test -- contracts.test.ts --reporter=verbose
```

### 3. Check Backend Configuration
Verify in `TrashAnimal.Api/Program.cs`:
```csharp
// Must include JsonStringEnumConverter for enum serialization
.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
})
```

## Environment Variables

### `VITE_API_BASE_URL`
Default: `http://localhost:5080`

Override to test against a different API:
```bash
export VITE_API_BASE_URL=http://staging-api:5080
npm run test:run -- contracts.test.ts
```

## Extending for New Endpoints

When adding a new API endpoint:

1. **Add tests to both files** (Vitest + Playwright)
2. **Test the happy path:**
   ```typescript
   it('POST /example returns ExampleResponse', async () => {
     const response = await fetch(`${API_BASE_URL}/example`, {
       method: 'POST',
       headers: { 'Content-Type': 'application/json' },
       body: JSON.stringify({ name: 'test' }),
     });
     
     expect(response.status).toBe(200);
     const body = await response.json();
     expect(body).toHaveProperty('id');
   });
   ```

3. **Test error cases:**
   ```typescript
   it('handles invalid input gracefully', async () => {
     const response = await fetch(`${API_BASE_URL}/example`, {
       method: 'POST',
       body: JSON.stringify({}),
     });
     
     expect([400, 422]).toContain(response.status);
   });
   ```

4. **Test enums if present:**
   ```typescript
   it('Status enum is a string', async () => {
     const body = await response.json();
     expect(typeof body.status).toBe('string');
   });
   ```

## See Also

- [types.ts](src/api/types.ts) — TypeScript definitions for all API contracts
- [gamesApi.ts](src/api/gamesApi.ts) — API client implementation
- [GameCommandRequest.cs](../TrashAnimal.Api/Contracts/Requests/GameCommandRequest.cs) — Backend polymorphic union
- [TrashAnimal.Api/CLAUDE.md](../TrashAnimal.Api/CLAUDE.md) — Backend API documentation
