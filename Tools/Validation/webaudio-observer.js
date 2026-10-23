/* Passive test instrumentation, installed before the shipped Unity loader.
 * Native audio calls keep their arguments, return values and exceptions. The
 * observer never creates a sound, starts/resumes a context, or changes a gain.
 * It adds a disconnected stereo analyser branch beside each real destination
 * connection. The original audible route remains connected and unchanged.
 */
(() => {
  "use strict";
  if (window.__firstGameAudioEvidence || !window.AudioNode) return;
  const connect = AudioNode.prototype.connect;
  const disconnect = AudioNode.prototype.disconnect;
  const start = AudioBufferSourceNode.prototype.start;
  const stop = AudioBufferSourceNode.prototype.stop;
  const contexts = new Map(), nodeIds = new WeakMap(), sourceEvents = new WeakMap();
  const errors = [];
  let nextNode = 1;
  const nodeId = node => {
    if (!nodeIds.has(node)) nodeIds.set(node, nextNode++);
    return nodeIds.get(node);
  };
  const guard = action => {
    try { action(); } catch (error) { errors.push(String(error)); }
  };
  function contextRecord(context) {
    if (!contexts.has(context)) {
      contexts.set(context, {id: contexts.size + 1, context,
        nodes: new Set(), destinationEdges: new Map(), connections: new Map()});
    }
    return contexts.get(context);
  }
  function observeConnection(source, destination, output = 0, input = 0) {
    const record = contextRecord(source.context);
    record.nodes.add(source);
    nodeId(source);
    if (!(destination instanceof AudioNode)) return;
    record.nodes.add(destination);
    nodeId(destination);
    const edge = [nodeId(source), nodeId(destination), output, input];
    record.connections.set(edge.join(":"), edge);
    if (destination !== source.context.destination) return;
    const outputs = record.destinationEdges.get(source) || new Map();
    if (!outputs.has(output)) {
      const splitter = source.context.createChannelSplitter(2);
      const analysers = [source.context.createAnalyser(), source.context.createAnalyser()];
      for (let channel = 0; channel < 2; channel++) {
        analysers[channel].fftSize = 2048;
        connect.call(splitter, analysers[channel], channel, 0);
      }
      connect.call(source, splitter, output, 0);
      outputs.set(output, {splitter, analysers});
      record.destinationEdges.set(source, outputs);
    }
  }
  function observeDisconnection(source, args) {
    const record = contexts.get(source.context);
    if (!record) return;
    const destination = args[0], sourceId = nodeId(source);
    if (destination !== undefined && typeof destination !== "number" && !(destination instanceof AudioNode)) return;
    const output = typeof destination === "number" ? destination : args[1];
    for (const [key, edge] of record.connections) {
      if (edge[0] !== sourceId) continue;
      if (destination instanceof AudioNode && edge[1] !== nodeId(destination)) continue;
      if (output !== undefined && edge[2] !== output) continue;
      if (args[2] !== undefined && edge[3] !== args[2]) continue;
      record.connections.delete(key);
    }
    const outputs = record.destinationEdges.get(source);
    if (!outputs || (destination instanceof AudioNode && destination !== source.context.destination)) return;
    for (const [number, probe] of [...outputs]) {
      if (output !== undefined && number !== output) continue;
      // disconnect() / disconnect(output) already removed the observer branch.
      // Only a destination-specific disconnect needs its parallel branch removed.
      if (destination === source.context.destination) disconnect.call(source, probe.splitter, number, 0);
      outputs.delete(number);
    }
    if (!outputs.size) record.destinationEdges.delete(source);
  }
  AudioNode.prototype.connect = function (...args) {
    const result = connect.apply(this, args);
    guard(() => observeConnection(this, ...args));
    return result;
  };
  AudioNode.prototype.disconnect = function (...args) {
    const result = disconnect.apply(this, args);
    guard(() => observeDisconnection(this, args));
    return result;
  };
  AudioBufferSourceNode.prototype.start = function (...args) {
    const result = start.apply(this, args);
    guard(() => {
      contextRecord(this.context).nodes.add(this);
      sourceEvents.set(this, {startedAtContextTime: this.context.currentTime,
        startArguments: args, stoppedAtContextTime: null});
    });
    return result;
  };
  AudioBufferSourceNode.prototype.stop = function (...args) {
    const result = stop.apply(this, args);
    guard(() => {
      const event = sourceEvents.get(this);
      if (event) event.stoppedAtContextTime = this.context.currentTime;
    });
    return result;
  };
  function snapshot() {
    return {wallTimeMs: performance.now(), errors: [...errors], contexts: [...contexts.values()].map(record => {
      const {context} = record;
      const readChannels = analysers => analysers.map(analyser => {
        const data = new Float32Array(analyser.fftSize);
        analyser.getFloatTimeDomainData(data);
        let squared = 0, peak = 0, nonfinite = 0;
        for (const value of data) {
          if (!Number.isFinite(value)) { nonfinite++; continue; }
          squared += value * value;
          peak = Math.max(peak, Math.abs(value));
        }
        return {samples: data.length, rms: Math.sqrt(squared / data.length), peak, nonfinite};
      });
      const destinationRoutes = [];
      for (const [source, outputs] of record.destinationEdges) {
        for (const [output, probe] of outputs) destinationRoutes.push({sourceNodeId: nodeId(source),
          output, channels: readChannels(probe.analysers)});
      }
      const buffers = [], gains = [];
      for (const node of record.nodes) {
        if (node instanceof AudioBufferSourceNode && node.buffer) {
          buffers.push({nodeId: nodeId(node), duration: node.buffer.duration,
            channels: node.buffer.numberOfChannels, sampleRate: node.buffer.sampleRate,
            loop: node.loop, playbackRate: node.playbackRate.value,
            ...(sourceEvents.get(node) || {})});
        }
        if (node instanceof GainNode) gains.push({nodeId: nodeId(node), value: node.gain.value});
      }
      return {id: record.id, state: context.state, currentTime: context.currentTime,
        sampleRate: context.sampleRate, destinationChannels: context.destination.channelCount,
        destinationNodeId: nodeId(context.destination),
        destinationConnections: [...record.destinationEdges.values()].reduce((n, values) => n + values.size, 0),
        destinationRoutes, buffers, gains, connections: [...record.connections.values()]};
    })};
  }
  Object.defineProperty(window, "__firstGameAudioEvidence", {value: Object.freeze({
    version: 1,
    snapshot,
    async sampleWindow(milliseconds = 1800, interval = 150) {
      if (!(milliseconds > 0 && milliseconds <= 10000 && interval >= 50 && interval <= milliseconds))
        throw new Error("Invalid bounded audio observation window");
      const result = [], until = performance.now() + milliseconds;
      do {
        result.push(snapshot());
        await new Promise(resolve => setTimeout(resolve, interval));
      } while (performance.now() < until);
      result.push(snapshot());
      return result;
    }
  }), configurable: false, writable: false});
})();
