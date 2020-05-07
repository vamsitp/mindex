// Credit: https://bl.ocks.org/d3noob/1a96af738c89b88723eb63456beb6510
// Credit: dirk.songuer@microsoft.com
function getWidth() {
    return Math.max(
        document.body.scrollWidth,
        document.documentElement.scrollWidth,
        document.body.offsetWidth,
        document.documentElement.offsetWidth,
        document.documentElement.clientWidth
    );
}

let treeData = /*placeholder*/;

// Collapse the node and all it's children
function collapse(d) {
    if (d.children) {
        d._children = d.children
        d._children.forEach(collapse)
        d.children = null
    }
}

function update(source) {

    // Compute the new tree layout.
    var nodes = treemap(root).descendants(),
        links = treemap(root).descendants().slice(1);

    // Normalize for fixed-depth.
    nodes.forEach(function (d) { d.y = d.depth * 240 });

    // ****************** Nodes section ***************************

    // Update the nodes...
    var node = svg.selectAll('g.node')
        .data(nodes, function (d) { return d.id || (d.id = ++i); });

    // Enter any new modes at the parent's previous position.
    var nodeEnter = node.enter().append('g')
        .attr('class', 'node')
        .attr("transform", function (d) {
            return "translate(" + source.y0 + "," + source.x0 + ")";
        })
        .on('click', click);

    // Add Circle for the nodes
    nodeEnter.append('circle')
        .attr('class', 'node')
        .attr('r', 1e-6)
        .style("fill", function (d) { return d._children ? "red" : "#fff"; })
        .style("stroke", function (d) { return d.children ? "steelblue" : (d.data.name.includes('R | ') ? "#c94c4c" : (d.data.name.includes('A | ') ? "#f18973" : (d.data.name.includes('C | ') ? "#87bdd8" : (d.data.name.includes('I | ') ? "#84bd84" : "darkgray")))); });

    nodeEnter.each(function (d) {
        var thisNode = d3.select(this);
        if (!d.children) {
            thisNode.append("a")
                .attr("xlink:href", function (d) { return d.data.url; })
                .attr("target", "_blank")
                .append("text")
                .attr("x", 15)
                .attr("dy", 3)
                .attr("text-anchor", "start")
                .text(function (d) { return d.data.name; })
                .style("fill", "black");
            //thisNode.append("input") // http://bl.ocks.org/biovisualize/3085882
            //    .attr("type", "checkbox")
            //    .attr("x", 35)
            //    .attr("dy", 3);
        } else {
            thisNode.append("text")
                .attr("x", -10)
                .attr("dy", -5)
                .attr("text-anchor", "end")
                .style("font-weight", "bold")
                .text(function (d) { return d.data.name; })
        }
    });

    // UPDATE
    var nodeUpdate = nodeEnter.merge(node);

    // Transition to the proper position for the node
    nodeUpdate.transition()
        .duration(duration)
        .attr("transform", function (d) {
            return "translate(" + d.y + "," + d.x + ")";
        });

    // Update the node attributes and style
    nodeUpdate.select('circle.node')
        .attr('r', 4.5)
        .style("fill", function (d) {
            return d._children ? "red" : "#fff";
        })
        .attr('cursor', 'pointer');

    nodeUpdate.select("text")
        .style("fill-opacity", 1);

    //.call(d3.drag()
    //    .on("start", dragstarted)
    //    .on("drag", dragged)
    //    .on("end", dragended));


    // Remove any exiting nodes
    var nodeExit = node.exit().transition()
        .duration(duration)
        .attr("transform", function (d) {
            return "translate(" + source.y + "," + source.x + ")";
        })
        .remove();

    // On exit reduce the node circles size to 0
    nodeExit.select('circle')
        .attr('r', 1e-6);

    // On exit reduce the opacity of text labels
    nodeExit.select('text')
        .style('fill-opacity', 1e-6);

    // ****************** links section ***************************

    // Update the links...
    var link = svg.selectAll('path.link')
        .data(links, function (d) { return d.id; });

    // Enter any new links at the parent's previous position.
    var linkEnter = link.enter().insert('path', "g")
        .attr("class", "link")
        .attr('d', function (d) {
            var o = { x: source.x0, y: source.y0 }
            return diagonal(o, o)
        });

    // UPDATE
    var linkUpdate = linkEnter.merge(link);

    // Transition back to the parent element position
    linkUpdate.transition()
        .duration(duration)
        .attr('d', function (d) { return diagonal(d, d.parent) });

    // Remove any exiting links
    var linkExit = link.exit().transition()
        .duration(duration)
        .attr('d', function (d) {
            var o = { x: source.x, y: source.y }
            return diagonal(o, o)
        })
        .remove();

    // Store the old positions for transition.
    nodes.forEach(function (d) {
        d.x0 = d.x;
        d.y0 = d.y;
    });

    // Creates a curved (diagonal) path from parent to the child nodes
    function diagonal(s, d) {

        path = `M ${s.y} ${s.x} C ${(s.y + d.y) / 2} ${s.x}, ${(s.y + d.y) / 2} ${d.x}, ${d.y} ${d.x}`;
        return path;
    }

    // Toggle children on click.
    function click(d) {
        if (d.children) {
            d._children = d.children;
            d.children = null;
        } else {
            d.children = d._children;
            d._children = null;
        }

        update(d);
    }

    //// https://bl.ocks.org/denisemauldin/538bfab8378ac9c3a32187b4d7aed2c2
    //// https://bl.ocks.org/micahstubbs/6b3eb08318df8d58d5c21dcccf3063f4
    //// https://stackoverflow.com/questions/50990010/adjust-link-start-end-point-according-to-node-position-in-d3-graph
}

// Set the dimensions and margins of the diagram
var margin = { top: 20, right: 70, bottom: 20, left: 240 },
    width = getWidth() - margin.right - margin.left,
    height = 2000 - margin.top - margin.bottom;

// append the svg object to the body of the page
// appends a 'group' element to 'svg'
// moves the 'group' element to the top left margin
var svg = d3.select("body").append("svg")
    .attr("width", width + margin.right + margin.left)
    .attr("height", height + margin.top + margin.bottom)
    .append("g")
    .attr("transform", "translate(" + margin.left + "," + margin.top + ")");

var i = 0,
    duration = 750,
    root;

// declares a tree layout and assigns the size
var treemap = d3.tree();
treemap.size([height, width]);

// Assigns parent, children, height, depth
root = d3.hierarchy(treeData); //, function (d) { return d.children; }); // treeData[0];
root.x0 = height / 2;
root.y0 = 0;

//// Collapse after the second level
// root.children.forEach(collapse);

update(root);