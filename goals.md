# SyncoStronbo - Product goals, user stories, and UML-ready specification

## 1. Product vision

SyncoStronbo is an Android application for synchronized multi-device light playback over a **star topology** on the **same LAN / Wi-Fi network**.

The product enables one **Host** device to coordinate one or more **Guest** devices so that supported outputs can react as closely in time as possible.

Confirmed output capabilities in scope:
- Flashlight
- Full-screen color / screen strobe
- Vibration / haptics

The long-term vision is to evolve SyncoStronbo into a **mini light show studio** where multiple connected devices can be orchestrated through triggers, target groups, and reusable sequences.

---

## 2. Priority-ordered product goals

### Priority 1 - Core synchronized session
1. Allow a user to create a room as **Host**.
2. Allow one or more users to join that room as **Guests**.
3. Allow the Host to send synchronized commands to connected Guests.
4. Optimize the system for **low-latency synchronization** on the same local network.

### Priority 2 - Device capability awareness
5. Each Guest device should provide a **capabilities list** to the system.
6. Capabilities may include flashlight, full-screen color / screen strobe, and vibration / haptics.
7. The Host should be able to target devices according to available capabilities.

### Priority 3 - Trigger-driven control
8. The Host should support **manual triggers**.
9. The Host should support **music / FFT-based triggers**.
10. The Host should support **predefined sequences**.
11. The Host should support **rule-based automation**.

### Priority 4 - Composition and show logic
12. The user should be able to define **target groups**.
13. A target group may include Guests, the Host itself, or both.
14. Triggers should be assignable to target groups.
15. The system should support effects such as stroboscope behavior with duration, frequency, and duty cycle.

---

## 3. Scope constraints

### Confirmed constraints
- Platform: **Android only for now**
- Network model: **same LAN / Wi-Fi required**
- Topology: **one Host, many Guests**
- Performance expectation: **low-latency synchronization is critical**

### Confirmed architectural direction
- The system must be extensible enough to work with multiple device capabilities.
- The synchronized session is centered on a Host-controlled room.
- Guests are controlled endpoints and may expose different output capabilities.

---

## 4. Actors

### Primary actors
- **Host**: creates and controls the room
- **Guest**: joins a room and executes synchronized outputs
- **Admin / Setup operator**: configures rooms, groups, triggers, and show settings
- **System / Trigger engine**: evaluates rules, sequences, FFT input, and dispatch conditions

---

## 5. Domain concepts

### Core concepts
- **Room**: a synchronized session managed by one Host and joined by multiple Guests
- **Host**: the controller of a room
- **Guest**: a participant device receiving synchronized commands
- **Target group**: a logical set of devices selected for one or more triggers
- **Trigger**: a condition or manual action that starts an effect or sequence
- **Sequence**: an ordered set of timed commands
- **Capability**: an output or behavior supported by a device
- **Effect**: a reusable output behavior such as flashlight pulse, screen strobe, or vibration pattern

### Confirmed capability examples
- Flashlight
- Full-screen color / screen strobe
- Vibration / haptics

---

## 6. UML-ready use-case model

### System boundary
**System:** SyncoStronbo

### Actors and use cases

#### Host
- Create room
- Close room
- Discover connected Guests
- View Guest status
- Send manual synchronized command
- Configure target groups
- Assign triggers to target groups
- Launch predefined sequence
- Start rule-based automation
- Start music / FFT trigger processing

#### Guest
- Join room
- Leave room
- Report capabilities
- Receive synchronized command
- Execute supported output

#### Admin / Setup operator
- Configure room behavior
- Configure target groups
- Configure effects
- Configure trigger rules
- Configure predefined sequences

#### System / Trigger engine
- Evaluate trigger conditions
- Process FFT / music events
- Map triggers to target groups
- Dispatch synchronized actions

---

## 7. UML-ready sequence scenarios

### 7.1 Host creates a room
1. Host launches room creation.
2. System creates a room session.
3. System starts room availability / join mechanism on the LAN.
4. Guests can discover or join the room.

### 7.2 Guest joins a room
1. Guest discovers an available room on the same LAN.
2. Guest requests to join the selected room.
3. System establishes the Host-Guest session.
4. Guest reports its capabilities.
5. Host sees the Guest as connected and available.

### 7.3 Manual synchronized trigger
1. Host activates a manual trigger.
2. System resolves the target group.
3. System checks target capabilities.
4. System dispatches synchronized commands to the selected devices.
5. Host and Guests execute the supported outputs.

### 7.4 Music / FFT trigger flow
1. Audio input is analyzed by the System / Trigger engine.
2. A configured FFT or frequency condition is detected.
3. The matching trigger rule is resolved.
4. Target groups linked to that trigger are selected.
5. The corresponding sequence or effect is dispatched.

### 7.5 Rule-based automation flow
1. The System / Trigger engine evaluates configured rules.
2. A rule condition becomes true.
3. The linked trigger is activated.
4. The target group is resolved.
5. The linked effect or sequence is executed.

---

## 8. UML-ready state model

### Room lifecycle states
- Draft / not created
- Hosting
- Joinable
- Guest connected
- Active synchronized session
- Closing
- Closed

### Guest lifecycle states
- Not connected
- Discovering room
- Joining
- Connected
- Reporting capabilities
- Ready for synchronization
- Leaving
- Disconnected

### Trigger lifecycle states
- Defined
- Armed
- Waiting for condition
- Fired
- Executing sequence / effect
- Completed

---

## 9. User stories

### Epic A - Room session management

#### Story A1 - Create room
**As a** Host  
**I want** to create a room  
**So that** Guests can join a synchronized session.

**Acceptance criteria**
- Given I am on Android and connected to the same LAN,
  when I create a room,
  then the room is available for Guests to join.
- Given a room exists,
  when I act as Host,
  then I remain the single controller of that room.

#### Story A2 - Join room
**As a** Guest  
**I want** to join a Host room  
**So that** I can receive synchronized commands.

**Acceptance criteria**
- Given a Host room is available on the same LAN,
  when I join it,
  then my device becomes part of the synchronized session.
- Given I have joined,
  when the session becomes active,
  then I can receive synchronized actions from the Host.

#### Story A3 - Leave room
**As a** Guest  
**I want** to leave a room  
**So that** I can stop participating in the session.

**Acceptance criteria**
- Given I am connected as a Guest,
  when I leave the room,
  then my device is removed from the active session.

#### Story A4 - Close room
**As a** Host  
**I want** to close a room  
**So that** the synchronized session ends for all Guests.

**Acceptance criteria**
- Given I am the Host,
  when I close the room,
  then the active room session ends.
- Given Guests are connected,
  when the room closes,
  then they are no longer part of that session.

### Epic B - Synchronized output control

#### Story B1 - Manual synchronized command
**As a** Host  
**I want** to send a manual command  
**So that** selected devices react at nearly the same time.

**Acceptance criteria**
- Given a room is active,
  when I trigger a manual command,
  then the system dispatches it to the selected targets.
- Given selected devices support the required output,
  when the command is executed,
  then those devices perform the output in a synchronized manner.

#### Story B2 - Capability-aware execution
**As a** Host  
**I want** the system to know each Guest's capabilities  
**So that** commands only target supported outputs.

**Acceptance criteria**
- Given a Guest joins a room,
  when it becomes ready,
  then its capabilities are available to the system.
- Given a target output is selected,
  when the command is prepared,
  then unsupported devices are not treated as valid targets for that output.

### Epic C - Grouping and composition

#### Story C1 - Create target groups
**As a** Admin / Setup operator  
**I want** to create target groups  
**So that** I can organize which devices respond to triggers.

**Acceptance criteria**
- Given a room or show configuration exists,
  when I create a target group,
  then it can contain Guests, the Host itself, or both.

#### Story C2 - Assign triggers to groups
**As a** Admin / Setup operator  
**I want** to assign triggers to target groups  
**So that** different devices can react differently to the same show logic.

**Acceptance criteria**
- Given a target group exists,
  when I assign a trigger to it,
  then the trigger can activate actions for that group.

### Epic D - Trigger engine

#### Story D1 - Music / FFT trigger
**As a** Host  
**I want** to trigger actions from music / FFT analysis  
**So that** the light show can react to audio events.

**Acceptance criteria**
- Given FFT analysis is enabled,
  when a configured frequency condition is detected,
  then the corresponding trigger can fire.

#### Story D2 - Predefined sequence
**As a** Host  
**I want** to launch predefined sequences  
**So that** I can replay reusable synchronized effects.

**Acceptance criteria**
- Given a predefined sequence exists,
  when I launch it,
  then the system executes its ordered actions on the configured targets.

#### Story D3 - Rule-based automation
**As a** Host  
**I want** rule-based triggers  
**So that** the system can react automatically to configured conditions.

**Acceptance criteria**
- Given a rule is configured,
  when its condition becomes true,
  then the linked trigger can execute.

#### Story D4 - Stroboscope effect
**As a** Host  
**I want** to configure a stroboscope effect  
**So that** devices can pulse for a duration with a given frequency and duty cycle.

**Acceptance criteria**
- Given a stroboscope effect is defined,
  when it is triggered,
  then the selected devices execute it using duration, frequency, and duty cycle settings.

---

## 10. Open decisions / to be specified later

The following topics were **not specified in detail yet** and should be clarified in later revisions instead of assumed:
- Exact synchronization tolerance or latency target
- Room security / authentication model
- Persistence model for rooms, triggers, sequences, and groups
- Export / import of show configurations
- Background execution guarantees and power-management constraints
- Whether future integrations such as external control APIs, MIDI, or DMX are required